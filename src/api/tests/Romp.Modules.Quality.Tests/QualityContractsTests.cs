using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Contracts;

namespace Romp.Modules.Quality.Tests;

public sealed class QualityContractsTests
{
    [Fact]
    [Trait("Spec", "AC-24")]
    public async Task Queries_NoData_ReturnEmpty()
    {
        using var host = new Support.TestHost(nameof(Queries_NoData_ReturnEmpty));
        await host.WithScopeAsync(async sp =>
        {
            var queries = new Romp.Modules.Quality.Infrastructure.Outcomes.QualityQueries(sp.GetRequiredService<Romp.Modules.Quality.Infrastructure.Persistence.QcDbContext>());

            Assert.Empty(await queries.GetReleasedQuantitiesAsync(1, CancellationToken.None));
            Assert.Empty(await queries.GetReleasedQuantitiesByBatchAsync(1, CancellationToken.None));
            Assert.Empty(await queries.GetReceivedQuantitiesAsync(1, CancellationToken.None));
            return true;
        });
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    [Trait("Spec", "AC-29")]
    public void Events_HaveEnvelopeAndSchemaV1()
    {
        QualityEvent[] events =
        [
            new GoodsReceiptRecordedEvent(1, "GRN-2026-00001", 2, 3, "PO-2026-00003", "DN-1", new DateOnly(2026, 9, 29), "Matched", 10, 10, []),
            new QcBatchAcceptedEvent(4, 5, 1, 3, "PO-2026-00003", "Accepted", []),
            new QcBatchRejectedEvent(4, 6, 1, 3, "PO-2026-00003", []),
            new QcBatchReworkRequestedEvent(4, 7, 1, 3, "PO-2026-00003", []),
            new QcInspectionRecordedEvent(4, 0, 1, 3, "PO-2026-00003", 1, "Aql", 100, 20, 1, 0, 0, 1, "Pass"),
        ];

        Assert.All(events, e =>
        {
            Assert.Equal(1, e.SchemaVersion);
            Assert.Equal(e.GetType().Name, e.EventType);
            Assert.NotEqual(Guid.Empty, e.EventId);
            Assert.Equal(3, e.PoId);
        });
        Assert.Equal(1, events[0].AggregateId); // the GRN
        Assert.Equal("GoodsReceipt", events[0].AggregateType);
        Assert.All(events[1..], e =>
        {
            Assert.Equal("QcBatch", e.AggregateType);
            Assert.Equal(4, e.AggregateId);
        });
    }
}

public sealed class QcOptionsTests
{
    [Fact]
    [Trait("Spec", "AC-46")]
    public void Defaults_MatchPlan()
    {
        var options = new QualityOptions();

        Assert.Equal(50, options.FullInspectionMaxLot);
        Assert.Equal("II", options.DefaultInspectionLevelCode);
        Assert.Equal("Aql2_5", options.DefaultAqlLevelCode);
        Assert.Equal(0, options.CriticalDefectTolerance);
        Assert.Equal(0, options.ReceiptFutureDays);
    }
}
