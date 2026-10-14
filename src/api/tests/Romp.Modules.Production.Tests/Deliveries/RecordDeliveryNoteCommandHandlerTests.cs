using Romp.Modules.Production.Tests.Milestones;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Production.Application.Deliveries;
using Romp.Modules.Production.Application.Milestones;
using Romp.Modules.Production.Application.ProductionRuns;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Tests.Support;

namespace Romp.Modules.Production.Tests.Deliveries;

public sealed class RecordDeliveryNoteCommandHandlerTests
{
    private static async Task<(TestHost Host, long RunId)> InProductionAsync(string name)
    {
        var host = new TestHost(name);
        var runId = await host.OpenRunAsync();
        await RecordMilestoneCommandHandlerTests.ApproveSampleAsync(host, runId);
        await host.SendAsync(new RecordMilestoneCommand(runId, MilestoneType.BulkCuttingStarted, new DateOnly(2026, 9, 10), Runs.Reporter, Runs.Person, null));
        return (host, runId);
    }

    private static RecordDeliveryNoteCommand Note(long runId, string dn, int size1, int size2, bool final = false, DateOnly? dispatch = null) => new(
        runId, dn, dispatch ?? new DateOnly(2026, 9, 25), final,
        new[] { new QuantityLineDto(1, 10, size1), new QuantityLineDto(2, 10, size2) }.Where(l => l.Qty > 0).ToList(), Runs.Person);

    [Fact]
    [Trait("Spec", "AC-21")]
    [Trait("Spec", "D4")]
    public async Task Handle_OverTolerance_RecordedAndFlagged()
    {
        var (host, runId) = await InProductionAsync(nameof(Handle_OverTolerance_RecordedAndFlagged));
        using var _ = host;

        var note = await host.SendAsync(Note(runId, "DN-1", 110, 50));

        Assert.Equal("Over", note.QuantityStatusCode);
        Assert.Equal(5, note.OverQty);
        Assert.Equal(1, await host.WithDbAsync(db => db.DeliveryNotes.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    [Trait("Spec", "D3")]
    public async Task Handle_FinalShort_DeliveredAndShort()
    {
        var (host, runId) = await InProductionAsync(nameof(Handle_FinalShort_DeliveredAndShort));
        using var _ = host;
        await host.SendAsync(Note(runId, "DN-1", 60, 30));

        var final = await host.SendAsync(Note(runId, "DN-2", 20, 10, final: true));

        Assert.Equal("Short", final.QuantityStatusCode);
        Assert.Equal(23, final.ShortQty);
        var events = await host.WithDbAsync(db => db.Set<OutboxMessage>().Where(m => m.EventType == "DeliveryNoteRecordedEvent").OrderBy(m => m.Id).ToListAsync());
        Assert.Equal(2, events.Count);
        Assert.Contains("\"PoOutcome\":\"PartiallyDelivered\"", events[0].Payload);
        Assert.Contains("\"PoOutcome\":\"Delivered\"", events[1].Payload);
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    [Trait("Spec", "NFR-FT-08")]
    public async Task Handle_NotificationDown_StillRecorded()
    {
        // Nothing in the recording path calls a notification service; the event is only a row in the same
        // transaction. With no dispatcher or consumer running at all, the note is recorded and its event waits.
        var (host, runId) = await InProductionAsync(nameof(Handle_NotificationDown_StillRecorded));
        using var _ = host;

        await host.SendAsync(Note(runId, "DN-1", 100, 50));

        var pending = await host.WithDbAsync(db => db.Set<OutboxMessage>().Where(m => m.EventType == "DeliveryNoteRecordedEvent").ToListAsync());
        var row = Assert.Single(pending);
        Assert.Null(row.ProcessedDte);
        Assert.Equal(runId, row.AggregateId);
    }

    [Fact]
    [Trait("Spec", "AC-23")]
    public async Task Handle_AsksForTheTermsInForceOnTheDispatchDate_NotTheEntryDate()
    {
        var (host, runId) = await InProductionAsync(nameof(Handle_AsksForTheTermsInForceOnTheDispatchDate_NotTheEntryDate));
        using var _ = host;
        var dispatch = new DateOnly(2026, 9, 22);
        host.PoQueries.TermsAt[dispatch] = FakePurchaseOrderQueries.DefaultTerms(revision: 3, expected: new DateOnly(2026, 9, 20), latest: new DateOnly(2026, 9, 24));

        var note = await host.SendAsync(Note(runId, "DN-1", 100, 50, dispatch: dispatch));

        Assert.Equal([dispatch], host.PoQueries.TermsAtRequests);
        Assert.Equal(3, note.RevisionNumber);
        Assert.Equal("Late", note.TimingCode);
        Assert.Equal(2, note.DaysLate);
    }

    [Fact]
    [Trait("Spec", "AC-20")]
    public async Task Handle_DuplicateVendorDnNo_Refused()
    {
        var (host, runId) = await InProductionAsync(nameof(Handle_DuplicateVendorDnNo_Refused));
        using var _ = host;
        await host.SendAsync(Note(runId, "DN-7", 10, 5));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Note(runId, "DN-7", 10, 5)));
        Assert.Equal(1, await host.WithDbAsync(db => db.DeliveryNotes.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public async Task Handle_EmptyLinesOrMissingDnNumber_FieldErrors()
    {
        var (host, runId) = await InProductionAsync(nameof(Handle_EmptyLinesOrMissingDnNumber_FieldErrors));
        using var _ = host;

        var noLines = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(
            new RecordDeliveryNoteCommand(runId, "DN-1", new DateOnly(2026, 9, 25), false, [], Runs.Person)));
        Assert.Contains("Lines", noLines.Errors.Keys);

        var noNumber = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Note(runId, "", 10, 5)));
        Assert.Contains("VendorDnNo", noNumber.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-19")]
    public async Task GetDetail_ShowsShippedToDateAgainstOrdered_WithToleranceLimits()
    {
        var (host, runId) = await InProductionAsync(nameof(GetDetail_ShowsShippedToDateAgainstOrdered_WithToleranceLimits));
        using var _ = host;
        await host.SendAsync(Note(runId, "DN-1", 40, 20));
        await host.SendAsync(Note(runId, "DN-2", 35, 10));

        var detail = await host.SendAsync(new GetProductionRunQuery(runId));

        Assert.Equal(2, detail.DeliveryNotes.Count);
        var size1 = detail.Quantities.Single(q => q.SizeId == 1);
        Assert.Equal((100, 75, 95, 105), (size1.Ordered, size1.Shipped, size1.MinRequired, size1.MaxAllowed));
        Assert.Equal(30, detail.Quantities.Single(q => q.SizeId == 2).Shipped);
    }

    [Fact]
    [Trait("Spec", "AC-34")]
    public async Task GetDetail_NeverExposesInternalPoData()
    {
        var (host, runId) = await InProductionAsync(nameof(GetDetail_NeverExposesInternalPoData));
        using var _ = host;

        var detail = await host.SendAsync(new GetProductionRunQuery(runId));

        // The DTO type itself has no place for cost, payment, notes or files; assert on its whole shape.
        var json = System.Text.Json.JsonSerializer.Serialize(detail);
        foreach (var forbidden in new[] { "UnitCost", "PaymentTerm", "AdvancePercent", "ImpactNote", "VendorMessage", "Cost" })
        {
            Assert.DoesNotContain(forbidden, json);
        }

        Assert.NotNull(detail.PoTerms);
        Assert.Equal(new DateOnly(2026, 10, 25), detail.PoTerms!.LatestAcceptableDate);
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public async Task SetExpectedCompletion_RecordsHistory_AndFlagsAtRiskInTheDetail()
    {
        var (host, runId) = await InProductionAsync(nameof(SetExpectedCompletion_RecordsHistory_AndFlagsAtRiskInTheDetail));
        using var _ = host;

        var first = await host.SendAsync(new SetExpectedCompletionCommand(runId, new DateOnly(2026, 10, 10), "initial", Runs.Person));
        await host.SendAsync(new SetExpectedCompletionCommand(runId, new DateOnly(2026, 10, 22), "vendor short of trims", Runs.Person));
        var detail = await host.SendAsync(new GetProductionRunQuery(runId));

        Assert.Null(first.OldDate);
        Assert.Equal(2, detail.ExpectedHistory.Count);
        Assert.Equal(new DateOnly(2026, 10, 10), detail.ExpectedHistory[1].OldDate);
        Assert.True(detail.IsAtRisk); // 22 Oct is later than 25 Oct minus the 7-day buffer
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public async Task SetExpectedCompletion_ReasonTooLongOrPersonMissing_FieldErrors()
    {
        var (host, runId) = await InProductionAsync(nameof(SetExpectedCompletion_ReasonTooLongOrPersonMissing_FieldErrors));
        using var _ = host;

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new SetExpectedCompletionCommand(runId, new DateOnly(2026, 10, 10), new string('x', 501), Runs.Person)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new SetExpectedCompletionCommand(runId, new DateOnly(2026, 10, 10), null, "")));
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    [Trait("Spec", "AC-27")]
    public async Task ProductionQueries_SumDeliveredPerSizeColour_AndReportApproval()
    {
        var (host, runId) = await InProductionAsync(nameof(ProductionQueries_SumDeliveredPerSizeColour_AndReportApproval));
        using var _ = host;
        await host.SendAsync(Note(runId, "DN-1", 40, 20));
        await host.SendAsync(Note(runId, "DN-2", 35, 10));

        await host.WithScopeAsync(async sp =>
        {
            var queries = new Romp.Modules.Production.Infrastructure.ProductionRuns.ProductionQueries(
                (Romp.Modules.Production.Infrastructure.Persistence.ProductionDbContext)sp.GetService(typeof(Romp.Modules.Production.Infrastructure.Persistence.ProductionDbContext))!);
            var delivered = (await queries.GetDeliveredQuantitiesAsync(FakePurchaseOrderQueries.PoId, CancellationToken.None)).OrderBy(d => d.SizeId).ToList();
            Assert.Equal([new DeliveredQuantity(1, 10, 75), new DeliveredQuantity(2, 10, 30)], delivered);
            Assert.True(await queries.HasApprovedPpSampleAsync(FakePurchaseOrderQueries.PoId, CancellationToken.None));
            return true;
        });
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    public async Task ProductionQueries_NoRun_ReturnEmptyAndFalse()
    {
        using var host = new TestHost(nameof(ProductionQueries_NoRun_ReturnEmptyAndFalse));

        await host.WithScopeAsync(async sp =>
        {
            var queries = new Romp.Modules.Production.Infrastructure.ProductionRuns.ProductionQueries(
                (Romp.Modules.Production.Infrastructure.Persistence.ProductionDbContext)sp.GetService(typeof(Romp.Modules.Production.Infrastructure.Persistence.ProductionDbContext))!);
            Assert.Empty(await queries.GetDeliveredQuantitiesAsync(999, CancellationToken.None));
            Assert.False(await queries.HasApprovedPpSampleAsync(999, CancellationToken.None));
            return true;
        });
    }
}
