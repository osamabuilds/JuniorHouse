using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Quality.Contracts;

namespace Romp.Api.IntegrationTests.Quality;

/// <summary>
/// SCRUM-197: the Sprint 4 flow through the real HTTP API against a real PostgreSQL, with the events between modules
/// delivered by the real dispatcher: PROD delivery note -> QC inbound delivery -> goods receipt -> batch -> inspection ->
/// safety checks -> split decision -> VNDR receipt progress and vendor scorecard. Also the status codes of each route
/// (404 unknown id, 400 field errors, 409 rule refusals).
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class ReceiveInspectFlowTests(ProductionApiFixture api)
{
    private const string Person = "Osama Shafique";

    private static DateOnly Today => ProductionApiFixture.Today;

    private static readonly object[] AllPass = Enumerable.Range(1, 6).Select(t => (object)new { checkTypeId = t, resultId = 1 }).ToArray();

    private static readonly object[] SafetyPass =
    [
        new { safetyTypeId = 1, resultId = 1, reason = (string?)null, affectedLines = Array.Empty<object>() },
        new { safetyTypeId = 2, resultId = 1, reason = (string?)null, affectedLines = Array.Empty<object>() },
    ];

    /// <summary>An acknowledged PO with a run, an approved sample, bulk cutting recorded, then the delivery notes given (size 4 / size 5, colour 1).</summary>
    private async Task<(long PoId, long RunId)> PoWithDeliveriesAsync(params (string Dn, int Size4, int Size5, bool Final)[] deliveries)
    {
        var (poId, _, runId) = await api.AcknowledgedPoWithRunAsync();
        Assert.True((await api.PostAsync($"/api/production/runs/{runId}/pp-rounds", new { reportedBy = "Ali Raza", actingPerson = Person })).IsSuccessStatusCode);
        Assert.True((await api.PostAsync($"/api/production/runs/{runId}/pp-rounds/1/decision", new { approve = true, actingPerson = Person })).IsSuccessStatusCode);
        Assert.True((await api.PostAsync($"/api/production/runs/{runId}/milestones", new { typeId = 6, claimedDate = Today, reportedBy = "Ali Raza", actingPerson = Person })).IsSuccessStatusCode);

        foreach (var (dn, size4, size5, final) in deliveries)
        {
            var response = await api.PostAsync($"/api/production/runs/{runId}/delivery-notes", new
            {
                vendorDnNo = dn,
                dispatchDate = Today,
                isFinalShipment = final,
                lines = new[] { new { sizeId = 4, colourId = 1, qty = size4 }, new { sizeId = 5, colourId = 1, qty = size5 } }.Where(l => l.qty > 0).ToArray(),
                actingPerson = Person,
            });
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }

        await api.DispatchAsync();
        return (poId, runId);
    }

    private async Task<JsonElement> OpenDeliveryAsync(string poNo, string dn)
    {
        var page = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/quality/inbound-deliveries?search={poNo}&pageSize=100"));
        return page.GetProperty("items").EnumerateArray().Single(d => d.GetProperty("vendorDnNo").GetString() == dn);
    }

    private static object[] CountedLines(int size4, int size5) =>
        [new { sizeId = 4, colourId = 1, qty = size4 }, new { sizeId = 5, colourId = 1, qty = size5 }];

    private async Task<string> PoNoAsync(long poId) =>
        (await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/purchase-orders/{poId}"))).GetProperty("poNo").GetString()!;

    [Fact]
    [Trait("Spec", "AC-1")]
    [Trait("Spec", "AC-10")]
    [Trait("Spec", "AC-29")]
    [Trait("Spec", "AC-35")]
    public async Task FullFlow_DeliveryNoteToGrnToBatchToSplitDecision_UpdatesReceiptProgressAndScorecard()
    {
        var (poId, _) = await PoWithDeliveriesAsync(("DN-A1", 60, 30, false), ("DN-A2", 40, 20, true));
        var poNo = await PoNoAsync(poId);
        var statusBefore = await api.PoStatusAsync(poId);

        // AC-1: both delivery notes are in the receiving queue, open.
        var queue = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/quality/inbound-deliveries?status=open&search={poNo}"));
        Assert.Equal(2, queue.GetProperty("totalCount").GetInt32());
        var delivery1 = await OpenDeliveryAsync(poNo, "DN-A1");
        Assert.Equal(90, delivery1.GetProperty("claimedTotal").GetInt32());

        // AC-3, AC-7: a GRN with a short count is recorded and flagged.
        var grn = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync("/api/quality/grns", new
        {
            inboundDeliveryId = delivery1.GetProperty("id").GetInt64(),
            receivedDate = Today,
            actingPerson = Person,
            note = (string?)null,
            lines = CountedLines(58, 30),
        }));
        Assert.Equal("Short", grn.GetProperty("reconciliationCode").GetString());
        Assert.Equal(-2, grn.GetProperty("diffTotal").GetInt32());
        Assert.Matches(@"^GRN-\d{4}-\d{5}$", grn.GetProperty("grnNo").GetString());

        // AC-12: the QC batch is open and pending, with the counted quantities.
        var batches = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/quality/batches?search={poNo}"));
        var batchId = Assert.Single(batches.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64();
        var batch = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/quality/batches/{batchId}"));
        Assert.Equal(88, batch.GetProperty("lotQty").GetInt32());

        // AC-13..AC-18: inspect (lot 88 at level II, AQL 2.5 is code E: sample 13, accept 0, reject 1), then the safety checks.
        var inspection = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync($"/api/quality/batches/{batchId}/inspections", new
        {
            inspectionDate = Today,
            actingPerson = Person,
            checks = AllPass,
            defects = Array.Empty<object>(),
        }));
        Assert.Equal(13, inspection.GetProperty("sampleQty").GetInt32());
        Assert.Equal("Pass", inspection.GetProperty("resultCode").GetString());
        var inspectionId = inspection.GetProperty("id").GetInt64();

        // AC-31: no acceptance before both safety checks are recorded.
        var early = await api.PostAsync($"/api/quality/batches/{batchId}/decision", new
        {
            actingPerson = Person,
            parts = new[] { new { outcomeId = 1, reason = (string?)null, lines = (object?)null } },
        });
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        var safety = await api.Client.PutAsJsonAsync($"/api/quality/inspections/{inspectionId}/safety-checks", new { actingPerson = Person, checks = SafetyPass }, ProductionApiFixture.Json);
        Assert.True(safety.IsSuccessStatusCode, await safety.Content.ReadAsStringAsync());

        // AC-22, AC-23: split, accept size 4 in full and reject size 5; the accepted part is released at once.
        var decision = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync($"/api/quality/batches/{batchId}/decision", new
        {
            actingPerson = Person,
            parts = new object[]
            {
                new { outcomeId = 1, reason = (string?)null, lines = new[] { new { sizeId = 4, colourId = 1, qty = 58 } } },
                new { outcomeId = 3, reason = "sleeves torn", lines = new[] { new { sizeId = 5, colourId = 1, qty = 30 } } },
            },
        }));
        Assert.Equal(2, decision.GetProperty("subBatches").GetArrayLength());

        await api.DispatchAsync(); // QC events reach VNDR

        await using var scope = api.Services.CreateAsyncScope();
        var released = await scope.ServiceProvider.GetRequiredService<IQualityQueries>().GetReleasedQuantitiesAsync(poId, CancellationToken.None);
        var releasedLine = Assert.Single(released);
        Assert.Equal((4, 58, "Accepted"), ((int)releasedLine.SizeId, releasedLine.Qty, releasedLine.OutcomeCode));

        // AC-40, AC-41: VNDR shows received against ordered and the PO status is untouched.
        var progress = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/purchase-orders/{poId}/receipts"));
        Assert.Equal(88, progress.GetProperty("receivedTotal").GetInt32());
        Assert.Equal(150, progress.GetProperty("orderedTotal").GetInt32());
        Assert.Equal(statusBefore, await api.PoStatusAsync(poId));

        // AC-35: the scorecard counts both delivery notes and the first-pass inspection.
        var vendorId = (await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/purchase-orders/{poId}"))).GetProperty("vendorId").GetInt64();
        var scorecard = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/vendors/{vendorId}/scorecard"));
        Assert.True(scorecard.GetProperty("deliveriesCounted").GetInt32() >= 2);
        Assert.True(scorecard.GetProperty("inspectedUnits").GetInt32() >= 13);
        Assert.Equal(JsonValueKind.Number, scorecard.GetProperty("onQuantityPct").ValueKind);

        // AC-4: a second GRN for the same delivery is refused; the queue now shows it received.
        var duplicate = await api.PostAsync("/api/quality/grns", new
        {
            inboundDeliveryId = delivery1.GetProperty("id").GetInt64(),
            receivedDate = Today,
            actingPerson = Person,
            note = (string?)null,
            lines = CountedLines(60, 30),
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var received = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/quality/inbound-deliveries?status=received&search={poNo}"));
        Assert.Equal(1, received.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    [Trait("Spec", "AC-42")]
    public async Task Routes_UnknownIds404_InvalidInput400_RuleRefusals409()
    {
        var (poId, _) = await PoWithDeliveriesAsync(("DN-B1", 100, 50, true));
        var poNo = await PoNoAsync(poId);
        var delivery = await OpenDeliveryAsync(poNo, "DN-B1");
        var deliveryId = delivery.GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync("/api/quality/grns/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync("/api/quality/batches/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync("/api/vendors/999999/scorecard")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync("/api/purchase-orders/999999/receipts")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync("/api/quality/grns", new
        {
            inboundDeliveryId = 999999, receivedDate = Today, actingPerson = Person, note = (string?)null, lines = CountedLines(1, 1),
        })).StatusCode);

        // 400 with field errors: acting person too short, negative quantity.
        var invalid = await api.PostAsync("/api/quality/grns", new
        {
            inboundDeliveryId = deliveryId, receivedDate = Today, actingPerson = "A", note = (string?)null, lines = CountedLines(-1, 5),
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var errors = (await invalid.Content.ReadAsStringAsync());
        Assert.Contains("actingPerson", errors, StringComparison.OrdinalIgnoreCase);

        // 409: a line that is on neither the delivery note nor the PO; a receipt date in the future.
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsync("/api/quality/grns", new
        {
            inboundDeliveryId = deliveryId, receivedDate = Today, actingPerson = Person, note = (string?)null,
            lines = new[] { new { sizeId = 4, colourId = 1, qty = 100 }, new { sizeId = 9, colourId = 9, qty = 1 } },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsync("/api/quality/grns", new
        {
            inboundDeliveryId = deliveryId, receivedDate = Today.AddDays(3), actingPerson = Person, note = (string?)null, lines = CountedLines(100, 50),
        })).StatusCode);

        // Nothing was stored by the refusals, so the valid one now succeeds.
        var grn = await api.PostAsync("/api/quality/grns", new
        {
            inboundDeliveryId = deliveryId, receivedDate = Today, actingPerson = Person, note = (string?)null, lines = CountedLines(100, 50),
        });
        Assert.Equal(HttpStatusCode.Created, grn.StatusCode);

        var batches = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/quality/batches?search={poNo}"));
        var batchId = Assert.Single(batches.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64();

        // AC-19: no outcome without an inspection. AC-17: a failed item needs a defect. AC-16: unknown batch.
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsync($"/api/quality/batches/{batchId}/decision", new
        {
            actingPerson = Person, parts = new[] { new { outcomeId = 3, reason = "not inspected", lines = (object?)null } },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsync($"/api/quality/batches/{batchId}/inspections", new
        {
            inspectionDate = Today, actingPerson = Person,
            checks = Enumerable.Range(1, 6).Select(t => (object)new { checkTypeId = t, resultId = t == 1 ? 2 : 1 }).ToArray(),
            defects = Array.Empty<object>(),
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.PostAsync("/api/quality/batches/999999/inspections", new
        {
            inspectionDate = Today, actingPerson = Person, checks = AllPass, defects = Array.Empty<object>(),
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PostAsync($"/api/quality/batches/{batchId}/inspections", new
        {
            inspectionDate = Today, actingPerson = "A", checks = AllPass, defects = Array.Empty<object>(),
        })).StatusCode);

        // The calculator preview and the paged lists answer.
        var plan = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync("/api/quality/aql/plan?lotQty=1000&inspectionLevel=II&aqlLevel=Aql2_5"));
        Assert.Equal((80, 5, 6), (plan.GetProperty("sampleQty").GetInt32(), plan.GetProperty("acceptNo").GetInt32(), plan.GetProperty("rejectNo").GetInt32()));
        var grns = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/quality/grns?search={poNo}&pageSize=500"));
        Assert.Equal(100, grns.GetProperty("pageSize").GetInt32()); // clamped
        var byPo = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/quality/grns/by-po/{poId}"));
        Assert.Equal(1, byPo.GetArrayLength());
    }
}
