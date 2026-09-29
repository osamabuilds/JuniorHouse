using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Application.Outcomes;
using Romp.Modules.Quality.Application.SafetyChecks;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Infrastructure.Outcomes;
using Romp.Modules.Quality.Infrastructure.Persistence;
using Romp.Modules.Quality.Tests.Support;

namespace Romp.Modules.Quality.Tests.Outcomes;

public sealed class OutcomeHandlerTests
{
    private static SafetyCheckDto SafePass(short type) => new(type, CheckResult.Pass, null, []);

    /// <summary>A GRN of 600 + 400, an inspection and (unless left out) both safety checks passed; returns the batch id and the inspection id.</summary>
    private static async Task<(long BatchId, long InspectionId)> ReadyAsync(TestHost host, string dn = "DN-1", bool safety = true)
    {
        var (_, batchId) = await host.RecordGrnAsync(dn, line1: 600, line2: 400);
        var inspection = await host.SendAsync(InspectionData.Command(batchId));
        if (safety)
        {
            await host.SendAsync(new RecordSafetyChecksCommand(
                inspection.Id, "Ali Raza", [SafePass(SafetyType.ChokingHazardTrims), SafePass(SafetyType.DrawstringNeckline)]));
        }

        return (batchId, inspection.Id);
    }

    private static DecisionPartDto Whole(short outcome, string? reason = null) => new(outcome, reason, null);

    private static async Task<IReadOnlyList<ReleasedLine>> ReleasedAsync(TestHost host, long poId = FakePurchaseOrderQueries.PoId) =>
        await host.WithScopeAsync(sp => new QualityQueries(sp.GetRequiredService<QcDbContext>()).GetReleasedQuantitiesAsync(poId, CancellationToken.None));

    [Fact]
    [Trait("Spec", "AC-22")]
    [Trait("Spec", "AC-23")]
    public async Task Handle_Split_PassingReleasedRejectedNot()
    {
        using var host = new TestHost(nameof(Handle_Split_PassingReleasedRejectedNot));
        var (batchId, _) = await ReadyAsync(host);

        var result = await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza",
        [
            new(QcOutcome.Accepted, null, [new(1, 10, 600), new(2, 10, 250)]),
            new(QcOutcome.Rejected, "torn seams", [new(2, 10, 150)]),
        ]));

        Assert.Equal(BatchStatus.Decided, result.BatchStatusId);
        var released = await ReleasedAsync(host);
        Assert.Equal([(1, 600), (2, 250)], released.Select(r => ((int)r.SizeId, r.Qty)).ToArray());
        Assert.All(released, r => Assert.Equal("Accepted", r.OutcomeCode));
    }

    [Fact]
    [Trait("Spec", "AC-23")]
    public async Task Handle_Split_ReworkDoesNotHoldAccepted()
    {
        using var host = new TestHost(nameof(Handle_Split_ReworkDoesNotHoldAccepted));
        var (batchId, _) = await host.RecordGrnAsync(line1: 600, line2: 400).ContinueWith(t => (t.Result.BatchId, 0L));
        var inspection = await host.SendAsync(InspectionData.Command(batchId));
        await host.SendAsync(new RecordSafetyChecksCommand(inspection.Id, "Ali Raza", [SafePass(SafetyType.ChokingHazardTrims), SafePass(SafetyType.DrawstringNeckline)]));

        var result = await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza",
        [
            new(QcOutcome.Accepted, null, [new(1, 10, 600), new(2, 10, 300)]),
            new(QcOutcome.Rework, "re-sew hems", [new(2, 10, 100)]),
        ]));

        Assert.Equal(BatchStatus.AwaitingRework, result.BatchStatusId);
        Assert.Equal(900, (await ReleasedAsync(host)).Sum(r => r.Qty)); // released while the 100 are still with the vendor
        Assert.Equal(BatchStatus.AwaitingRework, result.SubBatches.Single(s => s.OutcomeId == QcOutcome.Rework).StatusId);
    }

    [Fact]
    [Trait("Spec", "AC-20")]
    public async Task Handle_WholeBatch_NoLinesMeansEverything()
    {
        using var host = new TestHost(nameof(Handle_WholeBatch_NoLinesMeansEverything));
        var (batchId, _) = await ReadyAsync(host);

        var result = await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza", [Whole(QcOutcome.Accepted)]));

        var part = Assert.Single(result.SubBatches);
        Assert.Equal(1000, part.Lines.Sum(l => l.Qty));
        Assert.Equal("Accepted", part.OutcomeCode);
    }

    [Fact]
    [Trait("Spec", "AC-21")]
    public async Task Handle_Concession_TaggedInEvent()
    {
        using var host = new TestHost(nameof(Handle_Concession_TaggedInEvent));
        var (batchId, _) = await ReadyAsync(host);

        await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza", [Whole(QcOutcome.Concession, "slight shade variation, sold as-is")]));

        var released = await ReleasedAsync(host);
        Assert.All(released, r => Assert.Equal("Concession", r.OutcomeCode));
        var message = await host.WithDbAsync(db => db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.EventType == "QcBatchAcceptedEvent"));
        using var payload = JsonDocument.Parse(message.Payload);
        Assert.Equal("Concession", payload.RootElement.GetProperty("OutcomeCode").GetString());
        Assert.DoesNotContain("shade", message.Payload, StringComparison.Ordinal); // AC-29: reasons stay in QC
    }

    [Fact]
    [Trait("Spec", "AC-29")]
    [Trait("Spec", "NFR-FT-08")]
    public async Task Handle_WritesEventsInOneTransaction_WithTheIdsOfTheParts()
    {
        using var host = new TestHost(nameof(Handle_WritesEventsInOneTransaction_WithTheIdsOfTheParts));
        var (batchId, _) = await ReadyAsync(host);

        var result = await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza",
        [
            new(QcOutcome.Accepted, null, [new(1, 10, 600)]),
            new(QcOutcome.Rejected, "wrong colour", [new(2, 10, 300)]),
            new(QcOutcome.Rework, "fix labels", [new(2, 10, 100)]),
        ]));

        var messages = await host.WithDbAsync(db => db.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.EventType.StartsWith("QcBatch")).OrderBy(m => m.Id).ToListAsync());
        Assert.Equal(["QcBatchAcceptedEvent", "QcBatchRejectedEvent", "QcBatchReworkRequestedEvent"], messages.Select(m => m.EventType).ToArray());
        var subIds = messages.Select(m => JsonDocument.Parse(m.Payload).RootElement.GetProperty("SubBatchId").GetInt64()).ToArray();
        Assert.Equal(result.SubBatches.Select(s => s.Id).ToArray(), subIds);
        Assert.All(messages, m => Assert.Equal(batchId, m.AggregateId));
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    public async Task Handle_SplitDoesNotSum_RefusedAndNothingStored()
    {
        using var host = new TestHost(nameof(Handle_SplitDoesNotSum_RefusedAndNothingStored));
        var (batchId, _) = await ReadyAsync(host);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza",
        [
            new(QcOutcome.Accepted, null, [new(1, 10, 600), new(2, 10, 399)]),
        ])));

        Assert.Equal(0, await host.WithDbAsync(db => db.SubBatches.CountAsync()));
        Assert.Empty(await ReleasedAsync(host));
    }

    [Fact]
    [Trait("Spec", "AC-19")]
    public async Task Handle_NoInspection_Refused()
    {
        using var host = new TestHost(nameof(Handle_NoInspection_Refused));
        var (_, batchId) = await host.RecordGrnAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza", [Whole(QcOutcome.Rejected, "no")])));

        Assert.Contains("inspection", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public async Task Handle_AcceptWithoutSafetyChecks_Refused_ButRejectAllowed()
    {
        using var host = new TestHost(nameof(Handle_AcceptWithoutSafetyChecks_Refused_ButRejectAllowed));
        var (batchId, _) = await ReadyAsync(host, safety: false);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza", [Whole(QcOutcome.Accepted)])));
        Assert.Empty(await ReleasedAsync(host));

        var result = await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza", [Whole(QcOutcome.Rejected, "not safety checked")]));
        Assert.Equal(BatchStatus.Decided, result.BatchStatusId);
    }

    [Fact]
    [Trait("Spec", "AC-32")]
    public async Task Handle_FailedSafetyCheck_BlocksAcceptForTheAffectedLines()
    {
        using var host = new TestHost(nameof(Handle_FailedSafetyCheck_BlocksAcceptForTheAffectedLines));
        var (_, batchId) = await host.RecordGrnAsync(line1: 600, line2: 400);
        var inspection = await host.SendAsync(InspectionData.Command(batchId));
        await host.SendAsync(new RecordSafetyChecksCommand(inspection.Id, "Ali Raza",
        [
            new(SafetyType.ChokingHazardTrims, CheckResult.Fail, null, [new SafetyLineDto(2, 10)]),
            SafePass(SafetyType.DrawstringNeckline),
        ]));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza", [Whole(QcOutcome.Accepted)])));
        await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza",
        [
            new(QcOutcome.Accepted, null, [new(1, 10, 600)]),
            new(QcOutcome.Rejected, "buttons come off", [new(2, 10, 400)]),
        ]));

        Assert.Equal(600, (await ReleasedAsync(host)).Sum(r => r.Qty));
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public async Task Handle_SafetyResultsAreFrozenOnceDecided()
    {
        using var host = new TestHost(nameof(Handle_SafetyResultsAreFrozenOnceDecided));
        var (batchId, inspectionId) = await ReadyAsync(host);
        await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza", [Whole(QcOutcome.Accepted)]));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new RecordSafetyChecksCommand(
            inspectionId, "Ali Raza", [new(SafetyType.ChokingHazardTrims, CheckResult.Fail, null, []), SafePass(SafetyType.DrawstringNeckline)])));
    }

    [Fact]
    [Trait("Spec", "AC-25")]
    public async Task Handle_DecidedTwice_Refused()
    {
        using var host = new TestHost(nameof(Handle_DecidedTwice_Refused));
        var (batchId, _) = await ReadyAsync(host);
        await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza", [Whole(QcOutcome.Accepted)]));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza", [Whole(QcOutcome.Rejected, "second thoughts")])));

        Assert.Equal(1, await host.WithDbAsync(db => db.SubBatches.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-20")]
    public async Task Handle_InvalidInput_RefusedWithFieldErrors()
    {
        using var host = new TestHost(nameof(Handle_InvalidInput_RefusedWithFieldErrors));
        var (batchId, _) = await ReadyAsync(host);

        var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new DecideBatchCommand(batchId, "A", [])));

        Assert.Contains(error.Errors.Keys, k => k.Contains("ActingPerson", StringComparison.Ordinal));
        Assert.Contains(error.Errors.Keys, k => k.Contains("Parts", StringComparison.Ordinal));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new DecideBatchCommand(9999, "Ali Raza", [Whole(QcOutcome.Accepted)])));
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    public async Task Handle_ReworkReturnReinspectAndAccept_ReleasesTheReturnedQuantity()
    {
        using var host = new TestHost(nameof(Handle_ReworkReturnReinspectAndAccept_ReleasesTheReturnedQuantity));
        var (batchId, _) = await ReadyAsync(host);
        var decision = await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza",
        [
            new(QcOutcome.Accepted, null, [new(1, 10, 600), new(2, 10, 300)]),
            new(QcOutcome.Rework, "re-sew hems", [new(2, 10, 100)]),
        ]));
        var rework = decision.SubBatches.Single(s => s.OutcomeId == QcOutcome.Rework);

        var returned = await host.SendAsync(new RecordReworkReturnCommand(rework.Id, TestHost.Today, "Sara Khan", "back from vendor", [new(2, 10, 70)]));

        Assert.Equal(30, returned.ReworkPart.Lines.Single().OutstandingQty); // the shortfall stays visible (AC-27)
        Assert.Equal(BatchStatus.Pending, returned.ReturnedPart.StatusId);
        Assert.Equal(900, (await ReleasedAsync(host)).Sum(r => r.Qty)); // nothing more is released until re-inspected and decided

        var round = await host.SendAsync(InspectionData.Command(batchId, subBatchId: returned.ReturnedPart.Id));
        Assert.Equal((short)2, round.RoundNo);
        Assert.Equal(70, round.LotQty);
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new DecideSubBatchCommand(returned.ReturnedPart.Id, QcOutcome.Accepted, null, "Ali Raza"))); // safety checks needed again (AC-34)
        await host.SendAsync(new RecordSafetyChecksCommand(round.Id, "Ali Raza", [SafePass(SafetyType.ChokingHazardTrims), SafePass(SafetyType.DrawstringNeckline)]));
        var decided = await host.SendAsync(new DecideSubBatchCommand(returned.ReturnedPart.Id, QcOutcome.Accepted, null, "Ali Raza"));

        Assert.Equal("Accepted", Assert.Single(decided.SubBatches).OutcomeCode);
        Assert.Equal(970, (await ReleasedAsync(host)).Sum(r => r.Qty));
        var batch = await host.SendAsync(new GetQcBatchQuery(batchId));
        Assert.Equal(BatchStatus.AwaitingRework, batch.StatusId); // 30 units are still outstanding with the vendor
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    public async Task Handle_ReturnedMoreThanSent_Refused()
    {
        using var host = new TestHost(nameof(Handle_ReturnedMoreThanSent_Refused));
        var (batchId, _) = await ReadyAsync(host);
        var decision = await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza",
            [new(QcOutcome.Accepted, null, [new(1, 10, 600), new(2, 10, 300)]), new(QcOutcome.Rework, "re-sew", [new(2, 10, 100)])]));
        var rework = decision.SubBatches.Single(s => s.OutcomeId == QcOutcome.Rework);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new RecordReworkReturnCommand(rework.Id, TestHost.Today, "Sara Khan", null, [new(2, 10, 101)])));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new RecordReworkReturnCommand(9999, TestHost.Today, "Sara Khan", null, [new(2, 10, 1)])));
        var invalid = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new RecordReworkReturnCommand(rework.Id, TestHost.Today, "S", null, [])));
        Assert.Contains(invalid.Errors.Keys, k => k.Contains("Lines", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    public async Task Released_RejectedReworkPending_Excluded()
    {
        using var host = new TestHost(nameof(Released_RejectedReworkPending_Excluded));
        var (batchA, _) = await ReadyAsync(host, "DN-A"); // decided: accepted
        var (batchB, _) = await ReadyAsync(host, "DN-B"); // decided: rejected
        var (batchC, _) = await ReadyAsync(host, "DN-C"); // decided: rework
        await host.RecordGrnAsync("DN-D", 600, 400); // pending, never inspected
        await host.SendAsync(new DecideBatchCommand(batchA, "Ali Raza", [Whole(QcOutcome.Accepted)]));
        await host.SendAsync(new DecideBatchCommand(batchB, "Ali Raza", [Whole(QcOutcome.Rejected, "wrong fabric")]));
        await host.SendAsync(new DecideBatchCommand(batchC, "Ali Raza", [Whole(QcOutcome.Rework, "fix")]));

        var released = await ReleasedAsync(host);

        Assert.Equal([batchA], released.Select(r => r.BatchId).Distinct().ToArray());
        Assert.Equal(1000, released.Sum(r => r.Qty));
    }

    [Fact]
    [Trait("Spec", "AC-25")]
    public async Task Released_NeverExceedsCounted()
    {
        using var host = new TestHost(nameof(Released_NeverExceedsCounted));
        var (_, batchId) = await host.RecordGrnAsync(line1: 90, line2: 40); // counted less than claimed
        var inspection = await host.SendAsync(InspectionData.Command(batchId));
        await host.SendAsync(new RecordSafetyChecksCommand(inspection.Id, "Ali Raza", [SafePass(SafetyType.ChokingHazardTrims), SafePass(SafetyType.DrawstringNeckline)]));
        await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza", [Whole(QcOutcome.Accepted)]));

        var released = await host.WithScopeAsync(async sp =>
        {
            var queries = new QualityQueries(sp.GetRequiredService<QcDbContext>());
            var byBatch = await queries.GetReleasedQuantitiesByBatchAsync(batchId, CancellationToken.None);
            var received = await queries.GetReceivedQuantitiesAsync(FakePurchaseOrderQueries.PoId, CancellationToken.None);
            foreach (var line in byBatch)
            {
                Assert.True(line.Qty <= received.Single(r => r.SizeId == line.SizeId && r.ColourId == line.ColourId).Qty);
            }

            return byBatch;
        });

        Assert.Equal(130, released.Sum(r => r.Qty));
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    public void ReleaseInvariant_OnlyTheReleasePolicyDecidesWhatIsReleasable()
    {
        // The queries implementation is the only IQualityQueries; it filters through ReleasePolicy.ReleasableOutcomes (Accepted, Concession).
        var implementations = typeof(QualityQueries).Assembly.GetTypes().Where(t => typeof(IQualityQueries).IsAssignableFrom(t) && t.IsClass).ToList();

        Assert.Equal([typeof(QualityQueries)], implementations);
        Assert.Equal([QcOutcome.Accepted, QcOutcome.Concession], Domain.Outcomes.ReleasePolicy.ReleasableOutcomes.Order());
    }
}
