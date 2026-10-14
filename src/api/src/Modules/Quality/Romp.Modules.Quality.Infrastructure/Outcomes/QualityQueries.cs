using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.Outcomes;
using Romp.Modules.Quality.Domain.Receiving;
using Romp.Modules.Quality.Infrastructure.Persistence;

namespace Romp.Modules.Quality.Infrastructure.Outcomes;

/// <summary>
/// QC's public query surface (ADR 0002). Released quantity is exactly the lines of decided parts whose outcome
/// <see cref="ReleasePolicy"/> allows (Accepted and Concession), so Rejected, Rework, pending and undecided quantity is never
/// returned, and the quantity per size and colour can never exceed what was counted (spec AC-24, AC-25). Each answer is one SQL
/// statement, whatever the number of batches.
/// </summary>
public sealed class QualityQueries(QcDbContext dbContext) : IQualityQueries
{
    public async Task<IReadOnlyList<ReleasedLine>> GetReleasedQuantitiesAsync(long poId, CancellationToken cancellationToken)
    {
        var releasable = ReleasePolicy.ReleasableOutcomes.Select(o => (short?)o).ToArray();
        var batches = dbContext.Batches.AsNoTracking().Where(b => b.PoId == poId).Select(b => b.Id);
        return await (
                from s in dbContext.SubBatches.AsNoTracking()
                where releasable.Contains(s.OutcomeId) && batches.Contains(s.BatchId)
                join l in dbContext.Set<QcSubBatchLine>().AsNoTracking() on s.Id equals l.SubBatchId
                orderby s.BatchId, s.Id, l.SizeId, l.ColourId
                select new ReleasedLine(s.BatchId, s.Id, l.SizeId, l.ColourId, l.Qty, s.OutcomeId == QcOutcome.Accepted ? "Accepted" : "Concession"))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReleasedLine>> GetReleasedQuantitiesByBatchAsync(long batchId, CancellationToken cancellationToken)
    {
        var releasable = ReleasePolicy.ReleasableOutcomes.Select(o => (short?)o).ToArray();
        return await (
                from s in dbContext.SubBatches.AsNoTracking()
                where s.BatchId == batchId && releasable.Contains(s.OutcomeId)
                join l in dbContext.Set<QcSubBatchLine>().AsNoTracking() on s.Id equals l.SubBatchId
                orderby s.Id, l.SizeId, l.ColourId
                select new ReleasedLine(s.BatchId, s.Id, l.SizeId, l.ColourId, l.Qty, s.OutcomeId == QcOutcome.Accepted ? "Accepted" : "Concession"))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QcLine>> GetReceivedQuantitiesAsync(long poId, CancellationToken cancellationToken) =>
        await (
                from g in dbContext.GoodsReceipts.AsNoTracking()
                where g.PoId == poId
                join l in dbContext.Set<GoodsReceiptLine>().AsNoTracking() on g.Id equals l.GrnId
                group l by new { l.SizeId, l.ColourId } into grouped
                orderby grouped.Key.SizeId, grouped.Key.ColourId
                select new QcLine(grouped.Key.SizeId, grouped.Key.ColourId, grouped.Sum(x => x.CountedQty)))
            .ToListAsync(cancellationToken);
}
