using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Quality.Application.Outcomes;
using Romp.Modules.Quality.Application.SafetyChecks;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Infrastructure.Outcomes;
using Romp.Modules.Quality.Tests.Support;

namespace Romp.Modules.Quality.Tests.Outcomes;

/// <summary>The whole receive, inspect, decide and rework path on a real PostgreSQL (Testcontainers, CLAUDE.md): the joins behind the released-quantity queries, the ids the events carry, and the database-level guards.</summary>
public sealed class OutcomePostgresTests : IAsyncLifetime
{
    private QcPostgres _postgres = null!;

    public async Task InitializeAsync() => _postgres = await QcPostgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private static async Task<long> ReadyAsync(TestHost host, string dn)
    {
        var (_, batchId) = await host.RecordGrnAsync(dn, line1: 600, line2: 400);
        var inspection = await host.SendAsync(InspectionData.Command(batchId));
        await host.SendAsync(new RecordSafetyChecksCommand(inspection.Id, "Ali Raza",
        [
            new SafetyCheckDto(SafetyType.ChokingHazardTrims, CheckResult.Pass, null, []),
            new SafetyCheckDto(SafetyType.DrawstringNeckline, CheckResult.Pass, null, []),
        ]));
        return batchId;
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    [Trait("Spec", "AC-24")]
    [Trait("Spec", "AC-29")]
    public async Task Flow_SplitAndRework_ReleasedQuantityAndEventsOnPostgres()
    {
        using var host = new TestHost("pg", postgres: _postgres.ConnectionString);
        var batchId = await ReadyAsync(host, "DN-1");
        await ReadyAsync(host, "DN-2"); // a second batch, still undecided

        var decision = await host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza",
        [
            new(QcOutcome.Accepted, null, [new(1, 10, 600), new(2, 10, 200)]),
            new(QcOutcome.Concession, "minor shade variation", [new(2, 10, 100)]),
            new(QcOutcome.Rejected, "torn seams", [new(2, 10, 60)]),
            new(QcOutcome.Rework, "re-sew hems", [new(2, 10, 40)]),
        ]));

        await using var read = _postgres.NewContext();
        var queries = new QualityQueries(read);
        var released = await queries.GetReleasedQuantitiesAsync(FakePurchaseOrderQueries.PoId, CancellationToken.None);
        Assert.Equal(900, released.Sum(r => r.Qty)); // 600 + 200 accepted, 100 concession; rejected, rework and the undecided batch excluded
        Assert.Equal(["Accepted", "Accepted", "Concession"], released.Select(r => r.OutcomeCode).ToArray());
        Assert.Equal(1000, (await queries.GetReleasedQuantitiesByBatchAsync(batchId, CancellationToken.None)).Sum(r => r.Qty) + 60 + 40);
        Assert.Equal(2000, (await queries.GetReceivedQuantitiesAsync(FakePurchaseOrderQueries.PoId, CancellationToken.None)).Sum(r => r.Qty));

        var messages = await read.Set<OutboxMessage>().AsNoTracking().Where(m => m.EventType.StartsWith("QcBatch")).OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(
            ["QcBatchAcceptedEvent", "QcBatchAcceptedEvent", "QcBatchRejectedEvent", "QcBatchReworkRequestedEvent"],
            messages.Select(m => m.EventType).ToArray());
        Assert.All(messages, m => Assert.Equal(batchId, m.AggregateId));

        // The rework part comes back, is re-inspected with fresh safety checks and accepted.
        var rework = decision.SubBatches.Single(s => s.OutcomeId == QcOutcome.Rework);
        var returned = await host.SendAsync(new RecordReworkReturnCommand(rework.Id, TestHost.Today, "Sara Khan", null, [new(2, 10, 40)]));
        var round = await host.SendAsync(InspectionData.Command(batchId, subBatchId: returned.ReturnedPart.Id));
        await host.SendAsync(new RecordSafetyChecksCommand(round.Id, "Ali Raza",
        [
            new SafetyCheckDto(SafetyType.ChokingHazardTrims, CheckResult.Pass, null, []),
            new SafetyCheckDto(SafetyType.DrawstringNeckline, CheckResult.Pass, null, []),
        ]));
        await host.SendAsync(new DecideSubBatchCommand(returned.ReturnedPart.Id, QcOutcome.Accepted, null, "Ali Raza"));

        await using var after = _postgres.NewContext();
        Assert.Equal(940, (await new QualityQueries(after).GetReleasedQuantitiesAsync(FakePurchaseOrderQueries.PoId, CancellationToken.None)).Sum(r => r.Qty));
        Assert.Equal(BatchStatus.Decided, (await host.SendAsync(new GetQcBatchQuery(batchId))).StatusId);
    }

    [Fact]
    [Trait("Spec", "AC-25")]
    [Trait("Spec", "AC-43")]
    public async Task Decide_TwoAtOnce_OnlyOneWins()
    {
        using var host = new TestHost("pg", postgres: _postgres.ConnectionString);
        var batchId = await ReadyAsync(host, "DN-1");

        var results = await Task.WhenAll(
            Try(() => host.SendAsync(new DecideBatchCommand(batchId, "Ali Raza", [new(QcOutcome.Accepted, null, null)]))),
            Try(() => host.SendAsync(new DecideBatchCommand(batchId, "Sara Khan", [new(QcOutcome.Rejected, "wrong fabric", null)]))));

        Assert.Single(results, ok => ok);
        await using var check = _postgres.NewContext();
        Assert.Equal(1, await check.SubBatches.CountAsync()); // one decision, never two
    }

    private static async Task<bool> Try(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex) when (ex is Romp.BuildingBlocks.Domain.DomainException or DbUpdateException)
        {
            return false;
        }
    }
}
