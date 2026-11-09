using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>
/// What Fulfilment may read about an order (ADR 0030). Two queries (the order, its lines), projected to the contract's shape so no entity
/// leaves the module, and only the recipient's name and city: no phone, email or street address ever leave Orders (SEC-25).
/// </summary>
internal sealed class OrderFulfilmentQueries(OrdrDbContext dbContext) : IOrderFulfilmentQueries
{
    public async Task<OrderFulfilmentView?> FindAsync(long orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.AsNoTracking().Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.PlacementKey,
                o.PaymentMethodId,
                o.StatusId,
                o.RecipientName,
                o.CityName,
                o.Subtotal,
                o.Discount,
                o.Shipping,
                o.Total,
                o.PlacedAt,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null)
        {
            return null;
        }

        var lines = await dbContext.OrderLines.AsNoTracking().Where(l => l.OrderId == orderId).OrderBy(l => l.Id).ToListAsync(cancellationToken);
        var cod = PaymentMethods.IsCashOnDelivery(order.PaymentMethodId);

        return new OrderFulfilmentView(
            order.Id,
            order.OrderNumber,
            order.PlacementKey,
            PaymentMethods.CodeOf(order.PaymentMethodId),
            cod,
            OrderStatuses.CodeOf(order.StatusId),
            order.RecipientName,
            order.CityName,
            order.Subtotal,
            order.Discount,
            order.Shipping,
            order.Total,
            cod && order.StatusId != OrderStatuses.Cancelled ? order.Total : 0m,
            order.PlacedAt,
            [.. lines.Select(l => new OrderFulfilmentLine(l.VariantId, l.Sku, l.Name, l.Colour, l.Size, l.Quantity, l.UnitPrice, l.LineTotal))]);
    }
}
