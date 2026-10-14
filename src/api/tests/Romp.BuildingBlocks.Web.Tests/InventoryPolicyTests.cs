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
/// Warehouse plan G5 (owner decision D5): stock actions sit behind a capability-shaped policy that the development handler satisfies
/// today and a real role will satisfy in S8 (SEC-08, TC-STK-06).
/// </summary>
public sealed class InventoryPolicyTests
{
    private static async Task<WebApplication> StartAsync(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddRompAuthorization(builder.Environment);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSecuredGroup("/stock", Policies.Inventory).MapGet("/ping", () => Results.Ok("inventory"));
        await app.StartAsync();
        return app;
    }

    [Fact]
    [Trait("Spec", "TC-STK-06")]
    [Trait("Spec", "SEC-08")]
    public async Task TheInventoryPolicy_IsSatisfiedByTheDevelopmentUser_InTesting()
    {
        await using var app = await StartAsync("Testing");

        var response = await app.GetTestClient().GetAsync(new Uri("/stock/ping", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "TC-STK-06")]
    public void TheInventoryPolicy_HasItsOwnName_NotAnAliasOfAdmin() =>
        Assert.Equal("Inventory", Policies.Inventory);
}
