using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Romp.Api.IntegrationTests.Production;
using Romp.BuildingBlocks.Web;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>
/// The deployment gate on the real host (sessions task 13, AC-14, ADR 0011): a production-like environment starts only with the session
/// cookie scheme in place and only with the settings a deployment must supply, and never has the development handler.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class SecurityGateTests(ProductionApiFixture api)
{
    private static readonly string StrongKey = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 7)).ToArray());

    private WebApplicationFactory<Program> Staging(Action<IWebHostBuilder>? more = null, bool complete = true) =>
        api.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Staging");
            if (complete)
            {
                builder.UseSetting("Site:PublicBaseUrl", "https://staging.romp.example");
                builder.UseSetting("Identity:Csrf:Key", StrongKey);
            }

            more?.Invoke(builder);
        });

    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task Staging_StartsOnlyWithTheCookieScheme()
    {
        using var factory = Staging();
        using var client = factory.CreateClient();

        // The host started (the gate ran during start-up) and answers.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);

        var schemes = factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.NotNull(await schemes.GetSchemeAsync(RompAuthorizationExtensions.SessionScheme));
        Assert.Equal(RompAuthorizationExtensions.SessionScheme, (await schemes.GetDefaultAuthenticateSchemeAsync())?.Name);
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task DevHandler_IsAbsentOutsideDevelopment()
    {
        using var factory = Staging();
        using var client = factory.CreateClient();

        var schemes = await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        Assert.DoesNotContain(schemes, s => s.Name == RompAuthorizationExtensions.DevelopmentScheme);

        // Without a session every secured endpoint refuses; there is no stand-in user.
        foreach (var path in new[] { "/api/identity/staff", "/api/identity/roles", "/api/auth/me", "/api/catalog/styles" })
        {
            var response = await client.GetAsync(path);
            Assert.True(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound,
                $"{path} answered {(int)response.StatusCode} without a session");
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/identity/staff")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public void Staging_RefusesToStart_WithoutTheDeploymentSecrets()
    {
        // Neither the public origin nor the anti-forgery key is supplied.
        using var factory = Staging(complete: false);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    [Trait("Spec", "AC-14")]
    public void Staging_RefusesToStart_WithInsecureCookies()
    {
        using var factory = Staging(b => b.UseSetting("Identity:AllowInsecureCookies", "true"));

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public void Staging_RefusesToStart_WithRealAuthenticationSwitchedOff()
    {
        using var factory = Staging(b => b.UseSetting("Identity:UseRealAuthentication", "false"));

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public void Staging_RefusesToStart_WithABadSessionLifetime()
    {
        using var factory = Staging(b => b.UseSetting("Identity:Sessions:AccessMinutes", "0"));

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task Staging_SecurityHeaders_AndCookieNamesAreTheProductionOnes()
    {
        using var factory = Staging();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("__Secure-romp-acs", SessionCookieNamesFor(factory).Access);
    }

    private static Romp.Modules.Identity.Infrastructure.Sessions.SessionCookieNames SessionCookieNamesFor(WebApplicationFactory<Program> factory) =>
        Romp.Modules.Identity.Infrastructure.Sessions.SessionCookieNames.For(
            factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.AllowInsecureCookies);
}
