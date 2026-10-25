using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Storefront.Infrastructure;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

namespace Romp.Modules.Storefront.Tests.StorefrontSettings;

/// <summary>
/// SCRUM-206 task 12, ST-11 / SEC-08: every admin route is behind the StorefrontSettings policy (401 without a user, 403 without the
/// role), the public routes are explicitly anonymous, and the host will not expose the admin routes in production without real
/// authentication. The routes are the real ones from the module; authorization answers before any handler (or database) is reached.
/// </summary>
public sealed class StorefrontAdminSecurityTests
{
    private const string TestScheme = "TestRoles";

    private static async Task<WebApplication> StartAsync(string environment = "Production", bool enableAdminRoutes = true, bool realAuthentication = true)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Configuration["Storefront:EnableAdminRoutes"] = enableAdminRoutes ? "true" : "false";
        builder.Configuration["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused;Username=x;Password=x";
        builder.Services.AddRompAuthorization(builder.Environment);
        if (realAuthentication)
        {
            builder.Services.AddAuthentication(TestScheme).AddScheme<AuthenticationSchemeOptions, RoleHeaderHandler>(TestScheme, _ => { });
        }

        builder.Services.AddSharedPersistence();
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Application.AssemblyReference).Assembly));
        var module = new StorefrontModule();
        module.RegisterServices(builder.Services, builder.Configuration);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        module.MapEndpoints(app);
        await app.StartAsync();
        return app;
    }

    private static List<(string Method, string Path, RouteEndpoint Endpoint)> Routes(WebApplication app, string prefix) =>
        [.. app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith(prefix, StringComparison.Ordinal))
            .SelectMany(e => e.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods.Select(m => (
                m,
                e.RoutePattern.RawText!.Replace("{id:long}", "1", StringComparison.Ordinal).Replace("{slug}", "x", StringComparison.Ordinal),
                e)))];

    private static async Task<HttpStatusCode> SendAsync(WebApplication app, string method, string path, string? role)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (role is not null)
        {
            request.Headers.Add("X-Role", role);
        }

        if (method is "PUT" or "POST")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        return (await app.GetTestClient().SendAsync(request)).StatusCode;
    }

    [Fact]
    [Trait("Spec", "ST-11")]
    [Trait("Spec", "SEC-08")]
    public async Task EveryAdminRoute_HasTheStorefrontSettingsPolicy_AndThereAreRoutes()
    {
        await using var app = await StartAsync();

        var routes = Routes(app, StorefrontSettingsEndpoints.AdminPrefix);

        Assert.True(routes.Count >= 15);
        Assert.All(routes, r => Assert.Contains(r.Endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), a => a.Policy == Policies.StorefrontSettings));
        Assert.Contains(routes, r => r is { Method: "POST", Path: $"{StorefrontSettingsEndpoints.AdminPrefix}/publish" });
    }

    [Fact]
    [Trait("Spec", "ST-11")]
    [Trait("Spec", "SEC-08")]
    public async Task EveryAdminRoute_Is401WithoutAUser_And403ForAStaffUserWithoutTheRole()
    {
        await using var app = await StartAsync();

        foreach (var (method, path, _) in Routes(app, StorefrontSettingsEndpoints.AdminPrefix))
        {
            Assert.True(HttpStatusCode.Unauthorized == await SendAsync(app, method, path, null), $"{method} {path} without a user");
            Assert.True(HttpStatusCode.Forbidden == await SendAsync(app, method, path, "Staff"), $"{method} {path} as plain staff");
            Assert.True(HttpStatusCode.Forbidden == await SendAsync(app, method, path, "Catalogue"), $"{method} {path} with another capability role");
        }
    }

    [Fact]
    [Trait("Spec", "ST-11")]
    public async Task ThePublicRoutes_AreExplicitlyAnonymous_ThroughThePublicPolicy()
    {
        await using var app = await StartAsync();

        var routes = Routes(app, StorefrontSettingsEndpoints.PublicPrefix);

        Assert.Equal(8, routes.Count);
        Assert.All(routes, r => Assert.Equal("GET", r.Method));
        Assert.All(routes, r => Assert.Contains(r.Endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), a => a.Policy == Policies.Public));
        var policy = await app.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync(Policies.Public);
        Assert.Empty(policy!.AuthenticationSchemes);
        Assert.DoesNotContain(policy.Requirements, r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    [Trait("Spec", "ST-11")]
    public async Task WithTheSwitchOff_TheAdminRoutesAreNotMapped_ButThePublicOnesAre()
    {
        await using var app = await StartAsync(enableAdminRoutes: false);

        Assert.Empty(Routes(app, StorefrontSettingsEndpoints.AdminPrefix));
        Assert.Equal(8, Routes(app, StorefrontSettingsEndpoints.PublicPrefix).Count);
        Assert.Equal(HttpStatusCode.NotFound, await SendAsync(app, "GET", $"{StorefrontSettingsEndpoints.AdminPrefix}/draft", "Admin"));
    }

    [Fact]
    [Trait("Spec", "ST-11")]
    public async Task Production_WithTheSwitchOnAndNoRealAuthentication_RefusesToStart() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartAsync(realAuthentication: false));

    [Theory]
    [Trait("Spec", "ST-11")]
    [InlineData(true, false, false, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    public void Guard_RefusesOnlyWhenEnabledOutsideDevelopmentWithoutRealAuthentication(bool enabled, bool developmentLike, bool realAuthentication, bool refuses)
    {
        var verify = () => StorefrontAdminRoutesGuard.Verify(enabled, developmentLike, realAuthentication, "Production");

        if (refuses)
        {
            Assert.Throws<InvalidOperationException>(verify);
        }
        else
        {
            verify();
        }
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public void ThePolicyNames_AreNotAliasesOfAdmin()
    {
        Assert.Equal("StorefrontSettings", Policies.StorefrontSettings);
        Assert.Equal("Public", Policies.Public);
    }

    /// <summary>Signs a caller in with the roles named in the <c>X-Role</c> header; no header means no user (401).</summary>
    private sealed class RoleHeaderHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Role", out var roles))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                roles.ToString().Split(',').Select(r => new Claim(ClaimTypes.Role, r)).Append(new Claim(ClaimTypes.Name, "tester")),
                TestScheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
        }
    }
}
