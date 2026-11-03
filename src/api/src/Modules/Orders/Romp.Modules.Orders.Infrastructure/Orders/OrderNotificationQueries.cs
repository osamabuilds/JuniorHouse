using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>
/// What Notifications may read about an order (ADR 0026). One query for the order and its lines, projected to the contract's shape so no
/// entity leaves the module. Contact details are read here at send time and never travel in an event (SEC-25).
/// </summary>
internal sealed class OrderNotificationQueries(OrdrDbContext dbContext) : IOrderNotificationQueries
{
    public async Task<OrderNotificationView?> FindAsync(long orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.PaymentMethodId,
                o.ContactName,
                o.ContactEmail,
                o.ContactMobile,
                o.WhatsAppOptIn,
                o.Total,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null)
        {
            return null;
        }

        var lines = await dbContext.OrderLines
            .AsNoTracking()
            .Where(l => l.OrderId == orderId)
            .OrderBy(l => l.Id)
            .ToListAsync(cancellationToken);

        return new OrderNotificationView(
            order.Id,
            order.OrderNumber,
            PaymentMethods.CodeOf(order.PaymentMethodId),
            order.ContactName,
            order.ContactEmail,
            order.ContactMobile,
            order.WhatsAppOptIn,
            order.Total,
            lines.Select(l => new OrderNotificationLine(l.Name, l.Colour, l.Size, l.Quantity, l.LineTotal)).ToList());
    }
}
