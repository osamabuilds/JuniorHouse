using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.ProductRecommendations;

namespace Romp.Modules.Storefront.Application.ProductRecommendations;

/// <summary>
/// Copies the last <c>SalesWindowDays</c> of units sold (REC-4) from Orders, through its contract, into <c>PRDT_SALE_DAY</c>. The whole window is
/// rewritten each time, so a cancellation (which changes a past day) is picked up and running it twice gives the same rows. Days older than the
/// window are deleted. Returns the number of rows written or changed.
/// </summary>
public sealed record SyncProductSalesCommand : IRequest<int>;

public sealed class SyncProductSalesCommandHandler(IStorefrontReadDbContext db, IProductSalesQueries sales, IRecommendationSettings settings, TimeProvider clock)
    : IRequestHandler<SyncProductSalesCommand, int>
{
    public async Task<int> Handle(SyncProductSalesCommand request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var from = today.AddDays(1 - settings.SalesWindowDays);

        var reported = await sales.UnitsSoldByProductAndDayAsync(from, today, cancellationToken);
        var productIds = reported.Select(r => r.ProductId).Distinct().ToList();

        // Only products the read model holds can have a row (it is a foreign key); a product that is not published has no use for one.
        var known = (await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(cancellationToken)).ToHashSet();
        var wanted = reported.Where(r => known.Contains(r.ProductId) && r.Units > 0)
            .GroupBy(r => (r.ProductId, r.Day))
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Units));

        var existing = await db.ProductSaleDays.ToListAsync(cancellationToken);
        var changed = 0;
        foreach (var row in existing)
        {
            if (row.Day < from || !wanted.ContainsKey((row.ProductId, row.Day)))
            {
                db.ProductSaleDays.Remove(row);
                changed++;
            }
            else if (row.Units != wanted[(row.ProductId, row.Day)])
            {
                row.SetUnits(wanted[(row.ProductId, row.Day)]);
                changed++;
            }
        }

        var have = existing.Select(r => (r.ProductId, r.Day)).ToHashSet();
        foreach (var (key, units) in wanted.Where(w => !have.Contains(w.Key)))
        {
            db.ProductSaleDays.Add(new ProductSaleDay(key.ProductId, key.Day, units));
            changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }
}
