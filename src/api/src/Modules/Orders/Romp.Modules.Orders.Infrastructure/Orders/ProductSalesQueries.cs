using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>Units sold per product and UTC day for Storefront's recommendations (REC-4). One grouped query; cancelled orders do not count.</summary>
internal sealed class ProductSalesQueries(OrdrDbContext dbContext) : IProductSalesQueries
{
    public async Task<IReadOnlyList<ProductDayUnits>> UnitsSoldByProductAndDayAsync(DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken)
    {
        if (toDay < fromDay)
        {
            return [];
        }

        var start = new DateTimeOffset(fromDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(toDay.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var rows = await (
                from line in dbContext.OrderLines.AsNoTracking()
                join order in dbContext.Orders.AsNoTracking() on line.OrderId equals order.Id
                where order.StatusId != OrderStatuses.Cancelled && order.PlacedAt >= start && order.PlacedAt < end
                group line by new { line.ProductId, Day = DateOnly.FromDateTime(order.PlacedAt.UtcDateTime) } into g
                select new { g.Key.ProductId, g.Key.Day, Units = g.Sum(l => l.Quantity) })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ProductDayUnits(r.ProductId, r.Day, r.Units)).OrderBy(r => r.Day).ThenBy(r => r.ProductId).ToList();
    }
}
