using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Romp.BuildingBlocks.Web;

namespace Romp.BuildingBlocks.Web.Tests;

/// <summary>
/// Catalog plan G4 (Sprint 7): catalogue actions sit behind a capability-shaped policy that the development handler satisfies
/// today and a real role will satisfy in S8 (SEC-08).
/// </summary>
public sealed class CataloguePolicyTests
{
    private static async Task<WebApplication> StartAsync(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddRompAuthorization(builder.Environment);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSecuredGroup("/catalogue", Policies.Catalogue).MapGet("/ping", () => Results.Ok("catalogue"));
        await app.StartAsync();
        return app;
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public async Task TheCataloguePolicy_IsSatisfiedByTheDevelopmentUser_InTesting()
    {
        await using var app = await StartAsync("Testing");

        var response = await app.GetTestClient().GetAsync(new Uri("/catalogue/ping", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "SEC-08")]
    public void TheCataloguePolicy_HasItsOwnName_NotAnAliasOfAdmin() =>
        Assert.Equal("Catalogue", Policies.Catalogue);
}
