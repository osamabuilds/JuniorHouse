using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Returns.Domain;
using Romp.Modules.Returns.Domain.ReverseQc;
using Romp.Modules.Returns.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Returns;

/// <summary>
/// The returns routes through the real HTTP API against PostgreSQL (S17 R8). The development host signs the caller in with every permission; who may
/// call what is proved for every role by the authorization matrix and role behaviour tests, which include these routes.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class ReturnsEndpointsTests(ProductionApiFixture api)
{
    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    private static long _seq = 7_000_000;

    private HttpClient Client() => api.Factory.CreateClient();

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) => JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    /// <summary>A case awaiting receipt, as the shipment-returned consumer would have left it.</summary>
    private async Task<long> OpenCaseAsync(params int[] quantities)
    {
        var n = Interlocked.Increment(ref _seq);
        var created = ReturnCase.Open(n, $"RMP-RT{n}", ReturnKinds.Rto, [.. quantities.Select((q, i) => new ReturnLineSeed(n * 10 + i, $"SKU-{n}-{i}", "Frock", "Red", "4-5Y", q))]);
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RtrnDbContext>();
        db.Cases.Add(created);
        await db.SaveChangesAsync();
        return created.Id;
    }

    [Fact]
    [Trait("Spec", "RQC-2")]
    [Trait("Spec", "RQC-5")]
    [Trait("Spec", "RQC-7")]
    [Trait("Spec", "RQC-8")]
    [Trait("Spec", "RQC-11")]
    public async Task ACase_IsReceived_InspectedAndCompleted_ThroughTheRoutes()
    {
        using var client = Client();
        var id = await OpenCaseAsync(2, 1);

        var received = await client.PostAsJsonAsync($"/api/returns/cases/{id}/receive", new { sealIntact = false, binCode = "A-01-01" }, Json);
        var detail = await BodyAsync(received);
        var lineId = detail.GetProperty("lines")[0].GetProperty("id").GetInt64();
        var earlyComplete = await client.PostAsync($"/api/returns/cases/{id}/complete", null);
        var noNote = await client.PostAsJsonAsync($"/api/returns/cases/{id}/lines/{lineId}/decide", new { decision = "REJECT", quantity = 2 }, Json);
        var rejected = await client.PostAsJsonAsync($"/api/returns/cases/{id}/lines/{lineId}/decide", new { decision = "REJECT", quantity = 2, note = "Worn and washed" }, Json);
        var secondLineId = detail.GetProperty("lines")[1].GetProperty("id").GetInt64();
        await client.PostAsJsonAsync($"/api/returns/cases/{id}/lines/{secondLineId}/decide", new { decision = "REJECT", quantity = 1, note = "Not the item ordered" }, Json);
        var completed = await client.PostAsync($"/api/returns/cases/{id}/complete", null);
        var read = await BodyAsync(await client.GetAsync($"/api/returns/cases/{id}"));
        var list = await BodyAsync(await client.GetAsync("/api/returns/cases?status=COMPLETED&pageSize=100"));

        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        Assert.Equal("IN_INSPECTION", detail.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, earlyComplete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, noNote.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal("COMPLETED", read.GetProperty("status").GetString());
        Assert.Contains(list.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetInt64() == id);
    }

    [Fact]
    [Trait("Spec", "RQC-9")]
    public async Task ReceivingTwice_IsRefused()
    {
        using var client = Client();
        var id = await OpenCaseAsync(1);
        await client.PostAsJsonAsync($"/api/returns/cases/{id}/receive", new { sealIntact = false, binCode = "A-01-01" }, Json);

        var again = await client.PostAsJsonAsync($"/api/returns/cases/{id}/receive", new { sealIntact = false, binCode = "A-01-01" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    [Trait("Spec", "RQC-10")]
    public async Task ReceivingAnIntactParcelIntoAnUnknownBin_IsRefused_AndTheCaseIsStillAwaitingReceipt()
    {
        using var client = Client();
        var id = await OpenCaseAsync(1);

        var response = await client.PostAsJsonAsync($"/api/returns/cases/{id}/receive", new { sealIntact = true, binCode = "NO-SUCH-BIN" }, Json);
        var read = await BodyAsync(await client.GetAsync($"/api/returns/cases/{id}"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("AWAITING_RECEIPT", read.GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Spec", "RQC-12")]
    public async Task AnUnknownCase_IsNotFound_OnEveryRoute()
    {
        using var client = Client();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/returns/cases/987654321")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/returns/cases/987654321/receive", new { sealIntact = true, binCode = "A-01-01" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/returns/cases/987654321/lines/1/decide", new { decision = "RESTOCK", quantity = 1 }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/returns/cases/987654321/complete", null)).StatusCode);
    }
}
