using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Romp.BuildingBlocks.Web;

namespace Romp.BuildingBlocks.Web.Tests;

/// <summary>ADR 0011: the deployment gate, response headers, rate limiting and policies, tested on a tiny real host.</summary>
public sealed class SecurityBaselineTests
{
    private static WebApplication Build(string environment, Dictionary<string, string?>? settings = null, Action<IServiceCollection>? services = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings ?? []);
        builder.Services.AddRompAuthorization(builder.Environment);
        builder.Services.AddRompRateLimiting(builder.Configuration);
        services?.Invoke(builder.Services);
        var app = builder.Build();

        app.UseRompSecurityHeaders();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        var group = app.MapSecuredGroup("/secured");
        group.MapGet("/ping", () => Results.Ok("pong"));
        group.MapGet("/strict", () => Results.Ok("strict")).RequireRateLimiting(RateLimitPolicies.Strict);
        app.MapSecuredGroup("/admin", Policies.Admin).MapGet("/ping", () => Results.Ok("admin"));
        app.MapGet("/open", () => Results.Ok("open"));
        return app;
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("anything-else")]
    public async Task OutsideDevelopmentAndTesting_TheHostRefusesToStart_WithoutRealAuthentication(string environment)
    {
        await using var app = Build(environment);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SecurityGate.VerifyAsync(app.Services, app.Environment));

        Assert.Contains("Refusing to start", ex.Message, StringComparison.Ordinal);
        Assert.Contains(environment, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task InDevelopmentAndTesting_TheGateLetsTheHostStart(string environment)
    {
        await using var app = Build(environment);

        await SecurityGate.VerifyAsync(app.Services, app.Environment);
    }

    [Fact]
    public async Task InProduction_ARealAuthenticationScheme_OpensTheGate()
    {
        await using var app = Build("Production", services: s => s
            .AddAuthentication()
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, NoopHandler>("Staff-Login", _ => { }));

        await SecurityGate.VerifyAsync(app.Services, app.Environment);
    }

    [Fact]
    public async Task TheDevelopmentHandler_IsNotRegisteredOutsideDevelopmentAndTesting()
    {
        await using var production = Build("Production");
        await using var development = Build("Development");

        var prodSchemes = await production.Services.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        var devSchemes = await development.Services.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>().GetAllSchemesAsync();

        Assert.DoesNotContain(prodSchemes, s => s.Name == RompAuthorizationExtensions.DevelopmentScheme);
        Assert.Contains(devSchemes, s => s.Name == RompAuthorizationExtensions.DevelopmentScheme);
    }

    [Fact]
    public async Task SecuredEndpoints_WorkInDevelopment_AndEveryResponseCarriesTheBaselineHeaders()
    {
        await using var app = Build("Development");
        await app.StartAsync();
        var client = app.GetTestClient();

        var secured = await client.GetAsync("/secured/ping");
        var admin = await client.GetAsync("/admin/ping");
        var open = await client.GetAsync("/open");
        var missing = await client.GetAsync("/nothing-here");

        Assert.Equal(HttpStatusCode.OK, secured.StatusCode);
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        foreach (var response in new[] { secured, admin, open, missing })
        {
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        }
    }

    [Fact]
    public async Task TheStrictPolicy_StopsAClientThatHammersAnExpensiveEndpoint_WithoutBlockingNormalOnes()
    {
        await using var app = Build("Development", new Dictionary<string, string?> { ["RateLimit:StrictPerMinute"] = "3", ["RateLimit:PerMinute"] = "1000" });
        await app.StartAsync();
        var client = app.GetTestClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.GetAsync("/secured/strict")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], statuses);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/secured/ping")).StatusCode);
    }

    [Fact]
    public async Task TheGlobalLimit_AppliesToEveryEndpoint()
    {
        await using var app = Build("Development", new Dictionary<string, string?> { ["RateLimit:PerMinute"] = "2" });
        await app.StartAsync();
        var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/open")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/open")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/open")).StatusCode);
    }

    private sealed class NoopHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
    }
}
