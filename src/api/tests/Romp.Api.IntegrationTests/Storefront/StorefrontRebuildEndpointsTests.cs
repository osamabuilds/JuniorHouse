using System.Net;
using System.Text.Json;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Storefront;

/// <summary>Published-catalog task 15 through the real HTTP API: the rebuild starts with 202, runs in the background and reports its status (PCT-6).</summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class StorefrontRebuildEndpointsTests(ProductionApiFixture api)
{
    private const string Rebuild = "/api/admin/storefront/rebuild";

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task PostRebuild_Answers202WithTheRunsAddress_AndTheRunCompletesInTheBackground()
    {
        var response = await api.Client.PostAsync(Rebuild, null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var runId = (await ProductionApiFixture.ReadAsync<JsonElement>(response)).GetProperty("runId").GetInt64();
        Assert.EndsWith($"{Rebuild}/{runId}", response.Headers.Location?.ToString(), StringComparison.Ordinal);

        var status = "RUNNING";
        for (var i = 0; i < 60 && status == "RUNNING"; i++)
        {
            await Task.Delay(500);
            var run = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"{Rebuild}/{runId}"));
            status = run.GetProperty("status").GetString()!;
        }

        Assert.Equal("COMPLETED", status);
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task GetRebuild_UnknownRun_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync($"{Rebuild}/999999")).StatusCode);
    }
}
