using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Application.Receiving;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Tests.Support;

namespace Romp.Modules.Quality.Tests.Inspections;

public sealed class OpenQcBatchOnGoodsReceiptHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Handle_GrnRecorded_OneBatchPendingWithLines()
    {
        using var host = new TestHost(nameof(Handle_GrnRecorded_OneBatchPendingWithLines));

        var (grn, batchId) = await host.RecordGrnAsync(line1: 90, line2: 50);

        var batch = await host.WithDbAsync(db => db.Batches.AsNoTracking().Include(b => b.Lines).SingleAsync(b => b.Id == batchId));
        Assert.Equal(grn.Id, batch.GrnId);
        Assert.Equal(BatchStatus.Pending, batch.StatusId);
        Assert.Equal(140, batch.LotQty);
        Assert.Equal([(1, 90), (2, 50)], batch.Lines.OrderBy(l => l.SizeId).Select(l => ((int)l.SizeId, l.Qty)).ToArray());
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Handle_Twice_OneBatch()
    {
        using var host = new TestHost(nameof(Handle_Twice_OneBatch));
        var (grn, _) = await host.RecordGrnAsync();

        var pending = await host.WithScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IPublisher>().Publish(new GoodsReceiptRecordedNotification(grn.Id));
            return sp.GetRequiredService<Romp.Modules.Quality.Infrastructure.Persistence.QcDbContext>().ChangeTracker.HasChanges();
        });

        Assert.False(pending); // the handler found the batch and added nothing
        Assert.Equal(1, await host.WithDbAsync(db => db.Batches.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Handle_NothingCounted_NoBatch()
    {
        using var host = new TestHost(nameof(Handle_NothingCounted_NoBatch));

        var (grn, batchId) = await host.RecordGrnAsync(line1: 0, line2: 0);

        Assert.Equal("Short", grn.ReconciliationCode);
        Assert.Equal(0, batchId);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Handle_TwoGrns_TwoBatches()
    {
        using var host = new TestHost(nameof(Handle_TwoGrns_TwoBatches));

        await host.RecordGrnAsync("DN-1");
        await host.RecordGrnAsync("DN-2");

        Assert.Equal(2, await host.WithDbAsync(db => db.Batches.CountAsync()));
    }
}

public sealed class RecordInspectionCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-13")]
    [Trait("Spec", "AC-18")]
    public async Task Handle_Aql_StoresPlanAndResult()
    {
        using var host = new TestHost(nameof(Handle_Aql_StoresPlanAndResult));
        var (_, batchId) = await host.RecordGrnAsync(line1: 600, line2: 400);

        var inspection = await host.SendAsync(InspectionData.Command(
            batchId, defects: [InspectionData.Defect(DefectSeverity.Minor, 4), InspectionData.Defect(DefectSeverity.Major, 2)]));

        Assert.Equal("Aql", inspection.ModeCode);
        Assert.Equal((1000, 80, 5, 6), (inspection.LotQty, inspection.SampleQty, inspection.AcceptNo, inspection.RejectNo));
        Assert.Equal("Fail", inspection.ResultCode); // 6 major+minor defects reach the reject number
        Assert.Equal(6, inspection.Checks.Count);
        Assert.Equal(2, inspection.Defects.Count);
        var batch = await host.SendAsync(new GetQcBatchQuery(batchId));
        Assert.Equal(BatchStatus.InInspection, batch.StatusId);
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task Handle_SmallLot_FullInspection()
    {
        using var host = new TestHost(nameof(Handle_SmallLot_FullInspection));
        var (_, batchId) = await host.RecordGrnAsync(line1: 30, line2: 15);

        var inspection = await host.SendAsync(InspectionData.Command(batchId));

        Assert.Equal("Full", inspection.ModeCode);
        Assert.Equal(45, inspection.SampleQty);
        Assert.Equal("Pass", inspection.ResultCode);
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public async Task Handle_WritesEventInOneTransaction()
    {
        using var host = new TestHost(nameof(Handle_WritesEventInOneTransaction));
        var (grn, batchId) = await host.RecordGrnAsync(line1: 600, line2: 400);

        await host.SendAsync(InspectionData.Command(batchId, defects: [InspectionData.Defect(DefectSeverity.Critical, 1)]));

        var message = await host.WithDbAsync(db => db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.EventType == "QcInspectionRecordedEvent"));
        using var payload = JsonDocument.Parse(message.Payload);
        Assert.Equal(batchId, payload.RootElement.GetProperty("BatchId").GetInt64());
        Assert.Equal(grn.Id, payload.RootElement.GetProperty("GrnId").GetInt64());
        Assert.Equal("Fail", payload.RootElement.GetProperty("ResultCode").GetString());
        Assert.Equal(1, payload.RootElement.GetProperty("CriticalCount").GetInt32());
        Assert.Equal(batchId, message.AggregateId);
        Assert.DoesNotContain("loose thread", message.Payload, StringComparison.Ordinal); // defect notes never leave QC (AC-29)
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public async Task Handle_UnknownBatch_NotFound()
    {
        using var host = new TestHost(nameof(Handle_UnknownBatch_NotFound));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(InspectionData.Command(9999)));
    }

    [Fact]
    [Trait("Spec", "AC-17")]
    public async Task Handle_InvalidInput_RefusedWithFieldErrors_AndNothingStored()
    {
        using var host = new TestHost(nameof(Handle_InvalidInput_RefusedWithFieldErrors_AndNothingStored));
        var (_, batchId) = await host.RecordGrnAsync();

        var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(InspectionData.Command(
            batchId, defects: [InspectionData.Defect(DefectSeverity.Minor, 0, note: "")], person: "A")));

        Assert.Contains(error.Errors.Keys, k => k.Contains("ActingPerson", StringComparison.Ordinal));
        Assert.Contains(error.Errors.Keys, k => k.Contains("Qty", StringComparison.Ordinal));
        Assert.Contains(error.Errors.Keys, k => k.Contains("Note", StringComparison.Ordinal));
        Assert.Equal(0, await host.WithDbAsync(db => db.Inspections.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-17")]
    public async Task Handle_FailedItemWithoutDefect_RefusedAsConflict()
    {
        using var host = new TestHost(nameof(Handle_FailedItemWithoutDefect_RefusedAsConflict));
        var (_, batchId) = await host.RecordGrnAsync();

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(InspectionData.Command(batchId, checks: InspectionData.Checklist(CheckType.Stitching))));

        Assert.Equal(0, await host.WithDbAsync(db => db.Inspections.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public async Task Handle_SecondInspection_Refused()
    {
        using var host = new TestHost(nameof(Handle_SecondInspection_Refused));
        var (_, batchId) = await host.RecordGrnAsync();
        await host.SendAsync(InspectionData.Command(batchId));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(InspectionData.Command(batchId)));

        Assert.Equal(1, await host.WithDbAsync(db => db.Inspections.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task Handle_PreviewPlan_MatchesWhatTheInspectionWillApply()
    {
        using var host = new TestHost(nameof(Handle_PreviewPlan_MatchesWhatTheInspectionWillApply));

        var preview = await host.SendAsync(new PreviewAqlPlanQuery(1000, null, null));

        Assert.Equal(("J", 80, 5, 6, "Aql"), (preview.CodeLetter, preview.SampleQty, preview.AcceptNo, preview.RejectNo, preview.ModeCode));
    }

    [Fact]
    [Trait("Spec", "AC-45")]
    public async Task Handle_GetBatch_NeverExposesInternalPoNotes()
    {
        using var host = new TestHost(nameof(Handle_GetBatch_NeverExposesInternalPoNotes));
        var (_, batchId) = await host.RecordGrnAsync();

        var batch = await host.SendAsync(new GetQcBatchQuery(batchId));

        var names = typeof(QcBatchDto).GetProperties().Concat(typeof(QcBatchSummaryDto).GetProperties()).Select(p => p.Name);
        Assert.DoesNotContain(names, n => n.Contains("Cost", StringComparison.OrdinalIgnoreCase) || n.Contains("Internal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("UnitCost", JsonSerializer.Serialize(batch), StringComparison.Ordinal);
        Assert.Equal("GRN-2026-00001", batch.GrnNo);
    }
}
