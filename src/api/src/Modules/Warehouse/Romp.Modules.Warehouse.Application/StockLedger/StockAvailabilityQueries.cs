using Microsoft.EntityFrameworkCore;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Application.StockLedger;

/// <summary>
/// The Warehouse side of <see cref="IStockAvailabilityQueries"/> (published-catalog task 6): one query per call, however many variants it
/// returns. A variant appears once it has had a stock posting (its stock version is above zero); until then it is "not yet available".
/// </summary>
public sealed class StockAvailabilityQueries(IWarehouseDbContext dbContext) : IStockAvailabilityQueries
{
    /// <summary>The most variants one call returns.</summary>
    public const int MaxPageSize = 500;

    public async Task<IReadOnlyList<StockAvailability>> ListAvailabilityAsync(long afterVariantId, int take, CancellationToken cancellationToken) =>
        await Project(dbContext.VariantRefs.AsNoTracking()
                .Where(v => v.VariantId > afterVariantId && v.StockVersion > 0)
                .OrderBy(v => v.VariantId)
                .Take(Math.Clamp(take, 1, MaxPageSize)))
            .ToListAsync(cancellationToken);

    public async Task<StockAvailability?> GetAsync(long variantId, CancellationToken cancellationToken) =>
        await Project(dbContext.VariantRefs.AsNoTracking().Where(v => v.VariantId == variantId && v.StockVersion > 0))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<StockAvailability>> GetManyAsync(IReadOnlyCollection<long> variantIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variantIds);
        if (variantIds.Count > IStockAvailabilityQueries.MaxIds)
        {
            throw new ArgumentException($"At most {IStockAvailabilityQueries.MaxIds} variants can be asked for at once.", nameof(variantIds));
        }

        var ids = variantIds.Where(id => id > 0).Distinct().ToList();
        return ids.Count == 0
            ? []
            : await Project(dbContext.VariantRefs.AsNoTracking().Where(v => ids.Contains(v.VariantId) && v.StockVersion > 0)).ToListAsync(cancellationToken);
    }

    // Available is on-hand (all bins, all grades, LED-5) minus the units reserved for orders (RSV-7); the sum is a correlated subquery, so it is one statement.
    private IQueryable<StockAvailability> Project(IQueryable<VariantRef> source) =>
        source.Select(v => new StockAvailability(
            v.VariantId,
            v.SkuCode,
            Math.Max(0, (dbContext.Balances.Where(b => b.VariantRefId == v.Id).Sum(b => (int?)b.Quantity) ?? 0) - (dbContext.ReservedBalances.Where(r => r.Id == v.Id).Sum(r => (int?)r.Quantity) ?? 0)),
            v.StockVersion));
}
