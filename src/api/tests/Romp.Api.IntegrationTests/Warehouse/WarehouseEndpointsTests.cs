using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;

namespace Romp.Api.IntegrationTests.Warehouse;

/// <summary>
/// The warehouse features through the real HTTP API against a real PostgreSQL: routes, status codes (400 field errors, 404 unknown,
/// 409 rule refusals), the read-only stock surface, the blind count, and the Inventory-policy writes. Stock enters the way it does for
/// real: a variant arrives from Labelling and units are added by a found-stock adjustment (putaway needs a full QC flow, which the
/// module tests cover).
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class WarehouseEndpointsTests(ProductionApiFixture api)
{
    private const string Person = "Ayesha Khan";

    private static long _seed = 700_000;

    private static int _zone;

    private static string UniqueZone() => "H" + (Interlocked.Increment(ref _zone) % 256).ToString("X2", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<(long RefId, string Gtin, string Sku)> NewVariantAsync()
    {
        var n = Interlocked.Increment(ref _seed);
        var gtin = Gtin13.WithCheckDigit("5" + n.ToString("00000000000", System.Globalization.CultureInfo.InvariantCulture));
        var sku = $"ROMP-HTTP-{n}";
        await using var scope = api.Services.CreateAsyncScope();
        var refId = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecordVariantAssignedCommand(n, 9_000 + n, 10, 1, sku, gtin));
        return (refId, gtin, sku);
    }

    private async Task<(long Id, string Code)> NewBinAsync()
    {
        var created = await ProductionApiFixture.ReadAsync<JsonElement>(
            await api.PostAsync("/api/warehouse/bins", new { zone = UniqueZone(), aisle = "01", shelf = "A", actingPerson = Person }));
        return (created.GetProperty("id").GetInt64(), created.GetProperty("binCode").GetString()!);
    }

    private Task<HttpResponseMessage> Adjust(string gtin, string bin, int change, int reason, bool reconfirmed = false, Guid? key = null) =>
        api.PostAsync("/api/warehouse/adjustments", new
        {
            gtin, binCode = bin, gradeId = 1, quantityChange = change, reasonId = reason, note = "http test", reconfirmed,
            idempotencyKey = key ?? Guid.NewGuid(), actingPerson = Person,
        });

    private async Task<(string Gtin, string Sku, string Bin, long BinId, long RefId)> StockedAsync(int units)
    {
        var variant = await NewVariantAsync();
        var bin = await NewBinAsync();
        Assert.True((await Adjust(variant.Gtin, bin.Code, units, 4, reconfirmed: true)).IsSuccessStatusCode);
        return (variant.Gtin, variant.Sku, bin.Code, bin.Id, variant.RefId);
    }

    [Fact]
    [Trait("Spec", "ADJ-0")]
    [Trait("Spec", "LED-5")]
    public async Task AFoundStockAdjustment_PutsStockOnTheShelf_AndTheStockListShowsItWithItsBin()
    {
        var stocked = await StockedAsync(12);

        var list = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/warehouse/stock?search={stocked.Sku}"));

        var row = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(12, row.GetProperty("onHand").GetInt32());
        Assert.Equal(12, row.GetProperty("available").GetInt32());
        Assert.Equal(stocked.Bin, row.GetProperty("bins")[0].GetProperty("binCode").GetString());
    }

    [Fact]
    [Trait("Spec", "LED-10")]
    public async Task TheStockSurface_IsReadOnly_AnyWriteIsRefused()
    {
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch })
        {
            var response = await api.Client.SendAsync(new HttpRequestMessage(method, "/api/warehouse/stock") { Content = JsonContent.Create(new { onHand = 999 }) });
            Assert.True(response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, $"{method} /api/warehouse/stock returned {response.StatusCode}");
        }
    }

    [Fact]
    [Trait("Spec", "ADJ-1")]
    [Trait("Spec", "ADJ-4")]
    [Trait("Spec", "ADJ-7")]
    public async Task BadAdjustments_Are400Or409_WithProblemDetails_AndChangeNothing()
    {
        var stocked = await StockedAsync(3);

        Assert.Equal(HttpStatusCode.BadRequest, (await Adjust(stocked.Gtin, stocked.Bin, -1, reason: 0)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Adjust(stocked.Gtin, stocked.Bin, 0, reason: 2)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Adjust("123", stocked.Bin, -1, reason: 2)).StatusCode);
        var below = await Adjust(stocked.Gtin, stocked.Bin, -4, reason: 2);
        Assert.Equal(HttpStatusCode.Conflict, below.StatusCode);
        Assert.Contains("Not enough stock", await below.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, (await Adjust(stocked.Gtin, "NO-1-X", -1, reason: 2)).StatusCode);

        var list = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/warehouse/stock?search={stocked.Sku}"));
        Assert.Equal(3, Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("onHand").GetInt32());
    }

    [Fact]
    [Trait("Spec", "ADJ-4b")]
    [Trait("Spec", "TC-STK-09")]
    public async Task ALargeAdjustment_Is409ConfirmationRequired_ThenSucceedsWhenReconfirmed()
    {
        var stocked = await StockedAsync(100);

        var refused = await Adjust(stocked.Gtin, stocked.Bin, -30, reason: 3);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("Confirmation required", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var applied = await ProductionApiFixture.ReadAsync<JsonElement>(await Adjust(stocked.Gtin, stocked.Bin, -30, reason: 3, reconfirmed: true));
        Assert.Equal(70, applied.GetProperty("balanceAfter").GetInt32());
    }

    [Fact]
    [Trait("Spec", "ADJ-6")]
    [Trait("Spec", "ADJ-8")]
    public async Task AnAdjustment_CannotBeEditedOrDeleted_AndASecondSendWithTheSameKeyAppliesOnce()
    {
        var stocked = await StockedAsync(10);
        var key = Guid.NewGuid();

        var first = await ProductionApiFixture.ReadAsync<JsonElement>(await Adjust(stocked.Gtin, stocked.Bin, -2, reason: 1, key: key));
        var second = await ProductionApiFixture.ReadAsync<JsonElement>(await Adjust(stocked.Gtin, stocked.Bin, -2, reason: 1, key: key));

        Assert.Equal(first.GetProperty("id").GetInt64(), second.GetProperty("id").GetInt64());
        var id = first.GetProperty("id").GetInt64();
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch })
        {
            var response = await api.Client.SendAsync(new HttpRequestMessage(method, $"/api/warehouse/adjustments/{id}"));
            Assert.True(response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, $"{method} returned {response.StatusCode}");
        }

        var history = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/warehouse/adjustments?variantId={stocked.RefId}&reasonId=1"));
        Assert.Equal(1, history.GetProperty("totalCount").GetInt32());
        var reasons = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync("/api/warehouse/adjustments/reasons"));
        Assert.Equal(4, reasons.GetArrayLength());
    }

    [Fact]
    [Trait("Spec", "PUT-6")]
    [Trait("Spec", "PUT-17")]
    public async Task TheScanLookup_Is400ForAMalformedBarcode_404ForAnUnknownOne_AndAnswersForAKnownOne()
    {
        var variant = await NewVariantAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.GetAsync("/api/warehouse/putaway/scan/12345")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync($"/api/warehouse/putaway/scan/{Gtin13.WithCheckDigit("599999999999")}")).StatusCode);
        var found = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/warehouse/putaway/scan/{variant.Gtin}"));
        Assert.Equal(variant.Sku, found.GetProperty("skuCode").GetString());
        Assert.False(found.GetProperty("gtinRetired").GetBoolean());
    }

    [Fact]
    [Trait("Spec", "PUT-5")]
    [Trait("Spec", "PUT-14")]
    public async Task Putaway_QueueAndHistoryAreListed_AnUnknownItemIs404_AndABadQuantityIs400()
    {
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/warehouse/putaway/items?onlyOpen=true&pageSize=5")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/warehouse/putaway/records?pageSize=5")).StatusCode);
        var bin = await NewBinAsync();
        object Body(int qty) => new { binCode = bin.Code, quantity = qty, scanned = false, idempotencyKey = Guid.NewGuid(), actingPerson = Person };

        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync("/api/warehouse/putaway/items/999999999/confirm", Body(1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsync("/api/warehouse/putaway/items/1/confirm", Body(0))).StatusCode);
    }

    [Fact]
    [Trait("Spec", "CC-2")]
    [Trait("Spec", "CC-4")]
    [Trait("Spec", "CC-5")]
    public async Task ACycleCount_RunsOverHttp_TheBlindReadHidesTheSystemQuantity_AndConfirmingAdjustsTheVariance()
    {
        var stocked = await StockedAsync(10);

        var created = await ProductionApiFixture.ReadAsync<JsonElement>(
            await api.PostAsync("/api/warehouse/cycle-counts", new { binIds = new[] { stocked.BinId }, actingPerson = Person }));
        var id = created.GetProperty("id").GetInt64();

        var duplicate = await api.PostAsync("/api/warehouse/cycle-counts", new { binIds = new[] { stocked.BinId }, actingPerson = Person });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var put = await api.Client.PutAsJsonAsync($"/api/warehouse/cycle-counts/{id}/lines",
            new { binCode = stocked.Bin, lines = new[] { new { gtin = stocked.Gtin, gradeId = 1, quantity = 8 } }, recount = false, actingPerson = Person }, ProductionApiFixture.Json);
        var blind = await put.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.DoesNotContain("system", blind, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("variance", (await api.Client.GetStringAsync($"/api/warehouse/cycle-counts/{id}")), StringComparison.OrdinalIgnoreCase);

        var review = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/warehouse/cycle-counts/{id}/review"));
        var line = Assert.Single(review.GetProperty("lines").EnumerateArray());
        Assert.Equal((10, -2), (line.GetProperty("systemQuantity").GetInt32(), line.GetProperty("variance").GetInt32()));

        var noReason = await api.PostAsync($"/api/warehouse/cycle-counts/{id}/confirm", new { reasons = Array.Empty<object>(), actingPerson = Person });
        Assert.Equal(HttpStatusCode.Conflict, noReason.StatusCode);

        var closed = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync($"/api/warehouse/cycle-counts/{id}/confirm", new
        {
            reasons = new[] { new { binId = stocked.BinId, variantRefId = stocked.RefId, gradeId = 1, reasonId = 2, reconfirmed = false } },
            actingPerson = Person,
        }));
        Assert.Equal("CLOSED", closed.GetProperty("statusCode").GetString());

        var stock = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/warehouse/stock?search={stocked.Sku}"));
        Assert.Equal(8, Assert.Single(stock.GetProperty("items").EnumerateArray()).GetProperty("onHand").GetInt32());
    }

    [Fact]
    [Trait("Spec", "CC-11")]
    public async Task TheCountListAndBinAge_AreReadable_AndAnUnknownCountIs404()
    {
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/warehouse/cycle-counts?pageSize=5")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/warehouse/cycle-counts/bin-age?pageSize=5")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync("/api/warehouse/cycle-counts/999999999")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "ALR-6")]
    [Trait("Spec", "ALR-8")]
    public async Task Alerts_AreListed_TheBadgeCounts_AndAThresholdIsValidated()
    {
        var stocked = await StockedAsync(4);

        var badge = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync("/api/warehouse/alerts/open-count"));
        Assert.True(badge.GetProperty("open").GetInt32() >= 0);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/warehouse/alerts?pageSize=5")).StatusCode);

        var ok = await api.Client.PutAsJsonAsync($"/api/warehouse/alerts/thresholds/{stocked.RefId}", new { quantity = 2, actingPerson = Person }, ProductionApiFixture.Json);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.PutAsJsonAsync($"/api/warehouse/alerts/thresholds/{stocked.RefId}", new { quantity = -1, actingPerson = Person }, ProductionApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.PutAsJsonAsync("/api/warehouse/alerts/thresholds/999999999", new { quantity = 3, actingPerson = Person }, ProductionApiFixture.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync("/api/warehouse/alerts/999999999/acknowledge", new { actingPerson = Person })).StatusCode);
    }
}
