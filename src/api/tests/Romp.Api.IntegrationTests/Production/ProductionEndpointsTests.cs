using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Outbox;

namespace Romp.Api.IntegrationTests.Production;

/// <summary>
/// SCRUM-195: the Sprint 3 flow through the real HTTP API against a real PostgreSQL. Covers routes,
/// status codes (404 unknown id, 400 field errors, 409 rule refusals) and the cross-module event flow
/// (PO acknowledged -> run opened; bulk cutting -> PO In Production; delivery notes -> Partially Delivered / Delivered; close).
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class ProductionEndpointsTests(ProductionApiFixture api)
{
    private const string Person = "Osama Shafique";

    private static DateOnly Today => ProductionApiFixture.Today;

    private Task<HttpResponseMessage> Milestone(long runId, int typeId, DateOnly? date = null, string person = Person) =>
        api.PostAsync($"/api/production/runs/{runId}/milestones", new { typeId, claimedDate = date ?? Today, reportedBy = "Ali Raza", actingPerson = person });

    private async Task ApproveSampleAsync(long runId)
    {
        Assert.True((await api.PostAsync($"/api/production/runs/{runId}/pp-rounds", new { reportedBy = "Ali Raza", actingPerson = Person, note = "sample" })).IsSuccessStatusCode);
        Assert.True((await api.PostAsync($"/api/production/runs/{runId}/pp-rounds/1/decision", new { approve = true, actingPerson = Person })).IsSuccessStatusCode);
    }

    private Task<HttpResponseMessage> DeliveryNote(long runId, string dn, int size4, int size5, bool final = false) =>
        api.PostAsync($"/api/production/runs/{runId}/delivery-notes", new
        {
            vendorDnNo = dn,
            dispatchDate = Today,
            isFinalShipment = final,
            lines = new[] { new { sizeId = 4, colourId = 1, qty = size4 }, new { sizeId = 5, colourId = 1, qty = size5 } }.Where(l => l.qty > 0).ToArray(),
            actingPerson = Person,
        });

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task AcknowledgingAPo_OpensExactlyOneRun_WithNoMilestonesYet()
    {
        var (poId, poNo, runId) = await api.AcknowledgedPoWithRunAsync();

        var run = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/production/runs/{runId}"));

        Assert.Equal(poId, run.GetProperty("poId").GetInt64());
        Assert.Equal(poNo, run.GetProperty("poNo").GetString());
        Assert.Equal(1, run.GetProperty("statusId").GetInt32());
        Assert.Equal(0, run.GetProperty("milestones").GetArrayLength());
        Assert.Equal(3, await api.PoStatusAsync(poId)); // still Acknowledged: In Production waits for bulk cutting (D1)

        // A second dispatch pass (a redelivery) changes nothing.
        await api.DispatchAsync();
        var again = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/production/runs/by-po/{poId}"));
        Assert.Equal(runId, again.GetProperty("id").GetInt64());
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task BulkCutting_WithoutAnApprovedSample_Is409_NamingTheMissingApproval()
    {
        var (_, _, runId) = await api.AcknowledgedPoWithRunAsync();

        var response = await Milestone(runId, typeId: 6);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("pre-production sample has been approved", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Spec", "AC-35")]
    public async Task Milestone_WithoutTheActingPerson_Is400_WithAFieldError()
    {
        var (_, _, runId) = await api.AcknowledgedPoWithRunAsync();

        var response = await Milestone(runId, typeId: 1, person: "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ActingPerson", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task Milestone_DatedBeforeSendOrInTheFuture_Is409_WithTheAllowedRange()
    {
        var (_, _, runId) = await api.AcknowledgedPoWithRunAsync();

        var future = await Milestone(runId, typeId: 1, Today.AddDays(2));
        var past = await Milestone(runId, typeId: 1, Today.AddDays(-40));

        Assert.Equal(HttpStatusCode.Conflict, future.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, past.StatusCode);
        Assert.Contains(Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), await future.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task FabricBooked_IsRefusedWhenRompSuppliesTheFabric_AndDispatchIsRefusedWhenTheVendorDoes()
    {
        var (_, _, vendorRun) = await api.AcknowledgedPoWithRunAsync(fabricResponsibilityId: 1);
        var (_, _, rompRun) = await api.AcknowledgedPoWithRunAsync(fabricResponsibilityId: 2);

        Assert.True((await Milestone(vendorRun, 1)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Milestone(vendorRun, 2)).StatusCode);
        Assert.True((await Milestone(rompRun, 2)).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Milestone(rompRun, 1)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-36")]
    public async Task RompSupplied_CuttingBeforeFabricDispatch_IsRecordedWithALoudFlag()
    {
        var (_, _, runId) = await api.AcknowledgedPoWithRunAsync(fabricResponsibilityId: 2);
        await ApproveSampleAsync(runId);

        var cutting = await ProductionApiFixture.ReadAsync<JsonElement>(await Milestone(runId, typeId: 6));

        Assert.Contains("before Romp's fabric was dispatched", cutting.GetProperty("flagReason").GetString());
        var run = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/production/runs/{runId}"));
        Assert.True(run.GetProperty("isCuttingBeforeFabricDispatch").GetBoolean());
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    [Trait("Spec", "AC-12")]
    public async Task PpSample_RejectThenApproveWithAPhoto_KeepsBothRoundsAndSafeDownloadHeaders()
    {
        var (_, _, runId) = await api.AcknowledgedPoWithRunAsync();
        await api.PostAsync($"/api/production/runs/{runId}/pp-rounds", new { reportedBy = "Ali Raza", actingPerson = Person });

        var noReason = await api.PostAsync($"/api/production/runs/{runId}/pp-rounds/1/decision", new { approve = false, actingPerson = Person });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        Assert.True((await api.PostAsync($"/api/production/runs/{runId}/pp-rounds/1/decision", new { approve = false, reason = "neck too tight", actingPerson = Person })).IsSuccessStatusCode);
        Assert.True((await api.PostAsync($"/api/production/runs/{runId}/pp-rounds", new { reportedBy = "Ali Raza", actingPerson = Person })).IsSuccessStatusCode);

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        using var form = new MultipartFormDataContent { { new ByteArrayContent(png), "file", "sample-front.png" } };
        var upload = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.PostAsync($"/api/production/runs/{runId}/pp-rounds/2/files", form));
        var fileId = upload.GetProperty("id").GetInt64();

        // A disguised executable is refused with the reason.
        using var bad = new MultipartFormDataContent { { new ByteArrayContent([0x4D, 0x5A, 0x90, 0x00]), "file", "photo.jpg" } };
        var refused = await api.Client.PostAsync($"/api/production/runs/{runId}/pp-rounds/2/files", bad);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("isn't allowed", await refused.Content.ReadAsStringAsync());

        var download = await api.Client.GetAsync($"/api/production/runs/{runId}/pp-rounds/2/files/{fileId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.StartsWith("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(png, await download.Content.ReadAsByteArrayAsync());

        Assert.True((await api.PostAsync($"/api/production/runs/{runId}/pp-rounds/2/decision", new { approve = true, actingPerson = Person })).IsSuccessStatusCode);

        var rounds = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/production/runs/{runId}/pp-rounds"));
        Assert.Equal(2, rounds.GetArrayLength());
        Assert.Equal("Rejected", rounds[0].GetProperty("decisionCode").GetString());
        Assert.Equal("Approved", rounds[1].GetProperty("decisionCode").GetString());
        Assert.Equal(1, rounds[1].GetProperty("files").GetArrayLength());
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    [Trait("Spec", "AC-14")]
    [Trait("Spec", "AC-24")]
    [Trait("Spec", "AC-25")]
    public async Task FullFlow_Cutting_PartialDelivery_FinalShortDelivery_Close_MovesThePoThroughItsWholeLifecycle()
    {
        var (poId, _, runId) = await api.AcknowledgedPoWithRunAsync();
        await ApproveSampleAsync(runId);

        // Bulk cutting -> PO In Production (through PROD's event, handled by VNDR).
        Assert.True((await Milestone(runId, 6)).IsSuccessStatusCode);
        Assert.Equal(3, await api.PoStatusAsync(poId));
        await api.DispatchAsync();
        Assert.Equal(5, await api.PoStatusAsync(poId));

        // Expected completion; the run is at risk once it is later than the latest acceptable date minus the buffer.
        var expected = await api.Client.PutAsJsonAsync($"/api/production/runs/{runId}/expected-completion",
            new { date = Today.AddDays(28), reason = "vendor short of trims", actingPerson = Person }, ProductionApiFixture.Json);
        Assert.True(expected.IsSuccessStatusCode);
        var atRisk = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync("/api/production/runs?atRisk=true&search=" + "PO-"));
        Assert.Contains(atRisk.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetInt64() == runId);

        // DN 1: partial (60 of 100, 30 of 50) -> Partially Delivered.
        var dn1 = await ProductionApiFixture.ReadAsync<JsonElement>(await DeliveryNote(runId, "DN-1", 60, 30));
        Assert.Equal("OnQuantity", dn1.GetProperty("quantityStatusCode").GetString());
        await api.DispatchAsync();
        Assert.Equal(6, await api.PoStatusAsync(poId));

        // Same vendor DN number again: refused (409).
        Assert.Equal(HttpStatusCode.Conflict, (await DeliveryNote(runId, "DN-1", 1, 1)).StatusCode);

        // DN 2: final shipment, still short (needs 95 and 48) -> Delivered, marked short.
        var dn2 = await ProductionApiFixture.ReadAsync<JsonElement>(await DeliveryNote(runId, "DN-2", 20, 10, final: true));
        Assert.Equal("Short", dn2.GetProperty("quantityStatusCode").GetString());
        Assert.Equal(23, dn2.GetProperty("shortQty").GetInt32());
        await api.DispatchAsync();
        Assert.Equal(7, await api.PoStatusAsync(poId));

        var detail = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/production/runs/{runId}"));
        Assert.Equal(2, detail.GetProperty("deliveryNotes").GetArrayLength());
        var size4 = detail.GetProperty("quantities").EnumerateArray().Single(q => q.GetProperty("sizeId").GetInt32() == 4);
        Assert.Equal(80, size4.GetProperty("shipped").GetInt32());

        // A Delivered PO can no longer be amended or cancelled; closing is manual.
        Assert.Equal(HttpStatusCode.Conflict, (await api.PostAsync($"/api/purchase-orders/{poId}/cancel", new { cancelReasonId = 1 })).StatusCode);
        var closed = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.PostAsync($"/api/purchase-orders/{poId}/close", null));
        Assert.Equal(8, closed.GetProperty("statusId").GetInt32());
        await api.DispatchAsync();
        var closedRun = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/production/runs/{runId}"));
        Assert.Equal(3, closedRun.GetProperty("statusId").GetInt32()); // run closed with the PO
    }

    [Fact]
    [Trait("Spec", "AC-21")]
    [Trait("Spec", "D4")]
    public async Task OverDelivery_IsRecordedAndFlagged_Never409()
    {
        var (_, _, runId) = await api.AcknowledgedPoWithRunAsync();
        await ApproveSampleAsync(runId);
        await Milestone(runId, 6);

        var response = await DeliveryNote(runId, "DN-OVER", 120, 50); // size 4: ordered 100, allowed 105

        var note = await ProductionApiFixture.ReadAsync<JsonElement>(response);
        Assert.Equal("Over", note.GetProperty("quantityStatusCode").GetString());
        Assert.Equal(15, note.GetProperty("overQty").GetInt32());
    }

    [Fact]
    [Trait("Spec", "AC-28")]
    public async Task CancellingAPoInProduction_IsAllowedWithTheProductionReason_AndClosesTheRun()
    {
        var (poId, _, runId) = await api.AcknowledgedPoWithRunAsync();
        await ApproveSampleAsync(runId);
        await Milestone(runId, 6);
        await api.DispatchAsync();
        Assert.Equal(5, await api.PoStatusAsync(poId));

        var cancelled = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync($"/api/purchase-orders/{poId}/cancel", new { cancelReasonId = 7 }));
        await api.DispatchAsync();

        Assert.Equal(4, cancelled.GetProperty("statusId").GetInt32());
        var run = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/production/runs/{runId}"));
        Assert.Equal(2, run.GetProperty("statusId").GetInt32()); // cancelled
        Assert.Equal(HttpStatusCode.Conflict, (await Milestone(runId, 7)).StatusCode); // nothing more can be recorded
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    public async Task Amendment_BelowTheDeliveredQuantity_IsRefused()
    {
        var (poId, _, runId) = await api.AcknowledgedPoWithRunAsync();
        await ApproveSampleAsync(runId);
        await Milestone(runId, 6);
        await DeliveryNote(runId, "DN-A", 70, 0);
        await api.DispatchAsync();

        var response = await api.PostAsync($"/api/purchase-orders/{poId}/amendments", new
        {
            initiatorId = 1, reasonId = 1, impactNote = "cut the order", unitCost = 450m, expectedDeliveryDate = Today.AddDays(20),
            latestAcceptableDate = Today.AddDays(30), overTolerancePercent = 5m, underTolerancePercent = 5m,
            paymentTermId = 2, advancePercent = 0m, fabricResponsibilityId = 1,
            lines = new[] { new { sizeId = 4, colourId = 1, qty = 60 }, new { sizeId = 5, colourId = 1, qty = 50 } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("already delivered", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Spec", "AC-32")]
    public async Task List_IsPaged_ClampedAndUnknownIdsAre404()
    {
        for (var i = 0; i < 3; i++)
        {
            await api.AcknowledgedPoWithRunAsync();
        }

        var page = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync("/api/production/runs?page=1&pageSize=2"));
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 3);

        var clamped = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync("/api/production/runs?pageSize=100000"));
        Assert.Equal(100, clamped.GetProperty("pageSize").GetInt32());

        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync("/api/production/runs/99999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync("/api/production/runs/by-po/99999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Milestone(99999999, 1)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "AC-30")]
    public async Task OutboxRows_CarryTheRunAsAggregate_AndNoInternalNotes()
    {
        var (_, _, runId) = await api.AcknowledgedPoWithRunAsync();
        var response = await api.PostAsync($"/api/production/runs/{runId}/milestones", new
        {
            typeId = 1, claimedDate = Today, reportedBy = "SECRET-REPORTER", actingPerson = Person, note = "SECRET-NOTE",
        });
        Assert.True(response.IsSuccessStatusCode);

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Romp.Modules.Production.Infrastructure.Persistence.ProductionDbContext>();
        var reached = db.Set<OutboxMessage>().Where(m => m.EventType == "ProductionMilestoneReachedEvent" && m.AggregateId == runId).ToList();

        var row = Assert.Single(reached);
        Assert.DoesNotContain("SECRET", row.Payload);
        Assert.Contains("FabricBooked", row.Payload);
    }
}
