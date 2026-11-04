using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Romp.BuildingBlocks.Web;

namespace Romp.BuildingBlocks.Web.Tests;

/// <summary>
/// Sprint 8 task G4: endpoints declare the permission they need (SEC-08). The real handler arrives with the Identity module (task R2);
/// until then a development-only handler satisfies every permission for the development user, and any other handler (the real one,
/// or a test double here) decides per permission code.
/// </summary>
public sealed class RequirePermissionTests
{
    private static async Task<WebApplication> StartAsync(
        string environment, Action<IServiceCollection>? configure = null, Action<WebApplication>? map = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddRompAuthorization(builder.Environment);
        configure?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        if (map is null)
        {
            var group = app.MapSecuredGroup("/catalog", Permissions.CatalogRead);
            group.MapGet("/styles", () => Results.Ok("read"));
            group.MapPost("/styles", () => Results.Ok("write")).RequirePermission(Permissions.CatalogWrite);
        }
        else
        {
            map(app);
        }

        await app.StartAsync();
        return app;
    }

    /// <summary>Stands in for the Identity handler: grants every permission except the ones listed.</summary>
    private sealed class DenyingHandler(params string[] denied) : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            if (denied.Contains(requirement.Code.Value))
            {
                context.Fail();
            }
            else
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public async Task DevelopmentUser_SatisfiesEveryPermission_InTesting()
    {
        await using var app = await StartAsync("Testing");
        var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/catalog/styles", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(new Uri("/catalog/styles", UriKind.Relative), null)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public async Task ACallerWithoutThePermission_Gets403_AndTheBodyDoesNotNameIt()
    {
        await using var app = await StartAsync("Testing", services => services.AddSingleton<IAuthorizationHandler>(new DenyingHandler("catalog.write")));
        var client = app.GetTestClient();

        var allowed = await client.GetAsync(new Uri("/catalog/styles", UriKind.Relative));
        var forbidden = await client.PostAsync(new Uri("/catalog/styles", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.DoesNotContain("catalog", await forbidden.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public async Task ARouteAddingWrite_StillNeedsTheGroupsReadPermission()
    {
        await using var app = await StartAsync("Testing", services => services.AddSingleton<IAuthorizationHandler>(new DenyingHandler("catalog.read")));

        var response = await app.GetTestClient().PostAsync(new Uri("/catalog/styles", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public async Task EndpointMetadata_ExposesTheMostSpecificPermission_ForTheAuthorizationMatrix()
    {
        await using var app = await StartAsync("Testing");
        var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        string Effective(string method) => endpoints
            .Single(e => e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method))
            .Metadata.GetOrderedMetadata<PermissionMetadata>()[^1].Code.Value;

        Assert.Equal("catalog.read", Effective("GET"));
        Assert.Equal("catalog.write", Effective("POST"));
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public async Task AGroupDeclaredWithAPermission_RequiresAnAuthenticatedUser()
    {
        await using var app = await StartAsync("Testing");
        var endpoint = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().First();

        Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<PermissionMetadata>(), m => m.Code == Permissions.CatalogRead);
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public void TheDevelopmentPermissionHandler_IsRegisteredOnlyInDevelopmentAndTesting()
    {
        static bool HasDevHandler(string environment)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
            builder.Services.AddRompAuthorization(builder.Environment);
            return builder.Services.Any(d => d.ServiceType == typeof(IAuthorizationHandler) && d.ImplementationType?.Name == "DevelopmentPermissionHandler");
        }

        Assert.True(HasDevHandler("Testing"));
        Assert.True(HasDevHandler("Development"));
        Assert.False(HasDevHandler("Production"));
        Assert.False(HasDevHandler("Staging"));
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public async Task ThePolicyNameOverload_StillWorks_ForThePublicPolicy()
    {
        await using var app = await StartAsync("Testing", map: a => a.MapSecuredGroup("/legacy", Policies.Public).MapGet("/ping", () => Results.Ok("ok")));

        Assert.Equal(HttpStatusCode.OK, (await app.GetTestClient().GetAsync(new Uri("/legacy/ping", UriKind.Relative))).StatusCode);
    }
}
