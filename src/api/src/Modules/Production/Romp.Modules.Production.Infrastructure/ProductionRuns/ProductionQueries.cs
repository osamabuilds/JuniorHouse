using Microsoft.EntityFrameworkCore;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Production.Domain.Samples;
using Romp.Modules.Production.Infrastructure.Persistence;

namespace Romp.Modules.Production.Infrastructure.ProductionRuns;

/// <summary>The PROD side of <see cref="IProductionQueries"/> (ADR 0008): read-only, consistent at the moment VNDR asks (its amendment guard).</summary>
public sealed class ProductionQueries(ProductionDbContext dbContext) : IProductionQueries
{
    public async Task<IReadOnlyList<DeliveredQuantity>> GetDeliveredQuantitiesAsync(long poId, CancellationToken cancellationToken) =>
        await dbContext.DeliveryNoteLines.AsNoTracking()
            .Where(l => dbContext.DeliveryNotes.Any(n => n.Id == l.NoteId && dbContext.Runs.Any(r => r.Id == n.RunId && r.PoId == poId)))
            .GroupBy(l => new { l.SizeId, l.ColourId })
            .Select(g => new DeliveredQuantity(g.Key.SizeId, g.Key.ColourId, g.Sum(l => l.Qty)))
            .ToListAsync(cancellationToken);

    public async Task<bool> HasApprovedPpSampleAsync(long poId, CancellationToken cancellationToken) =>
        await dbContext.PpRounds.AsNoTracking()
            .AnyAsync(p => p.DecisionId == PpDecision.Approved && dbContext.Runs.Any(r => r.Id == p.RunId && r.PoId == poId), cancellationToken);
}
