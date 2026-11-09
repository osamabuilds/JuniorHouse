using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>
/// What Notifications may read about an order (ADR 0026). One query for the order and its lines, projected to the contract's shape so no
/// entity leaves the module. Contact details are read here at send time and never travel in an event (SEC-25).
/// </summary>
internal sealed class OrderNotificationQueries(OrdrDbContext dbContext, Application.Orders.ICodLinks links, Application.OrderLookup.ILookupLinks lookupLinks, IOptions<SiteOptions> site, TimeProvider clock) : IOrderNotificationQueries
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
                o.StatusId,
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

        // The reason category of the cancel, from the append-only history (OCF-4). Only the code leaves Orders, never the staff note.
        short? reasonId = await dbContext.OrderHistory
            .AsNoTracking()
            .Where(h => h.OrderId == orderId && h.ToStatusId == OrderStatuses.Cancelled)
            .OrderByDescending(h => h.Id)
            .Select(h => h.ReasonId)
            .FirstOrDefaultAsync(cancellationToken);

        return new OrderNotificationView(
            order.Id,
            order.OrderNumber,
            PaymentMethods.CodeOf(order.PaymentMethodId),
            order.ContactName,
            order.ContactEmail,
            order.ContactMobile,
            order.WhatsAppOptIn,
            order.Total,
            lines.Select(l => new OrderNotificationLine(l.Name, l.Colour, l.Size, l.Quantity, l.LineTotal)).ToList(),
            reasonId is { } id && CancelReasons.IsKnown(id) ? CancelReasons.CodeOf(id) : null,
            ConfirmUrl(order.StatusId, order.PaymentMethodId, order.OrderNumber),
            TrackUrl(order.OrderNumber));
    }

    /// <summary>
    /// The signed "Track your order" address (OLK, Q-OLK-4). The token identifies one order and the page behind it shows only status and
    /// masked data. Null when the public site address is not set.
    /// </summary>
    private string? TrackUrl(string orderNumber) =>
        string.IsNullOrWhiteSpace(site.Value.PublicBaseUrl)
            ? null
            : $"{site.Value.PublicBaseUrl.TrimEnd('/')}/track-order?token={Uri.EscapeDataString(lookupLinks.Create(orderNumber))}";

    /// <summary>
    /// The signed "Confirm my order" address for a cash on delivery order that is still waiting (COD-12). Null for any other order, or when the
    /// public site address is not set. The token identifies one order and nothing else.
    /// </summary>
    private string? ConfirmUrl(short statusId, short paymentMethodId, string orderNumber)
    {
        if (statusId != OrderStatuses.PendingConfirmation || !PaymentMethods.IsCashOnDelivery(paymentMethodId)
            || string.IsNullOrWhiteSpace(site.Value.PublicBaseUrl))
        {
            return null;
        }

        var token = links.Create(orderNumber, clock.GetUtcNow());
        return $"{site.Value.PublicBaseUrl.TrimEnd('/')}/cod/confirm?token={Uri.EscapeDataString(token)}";
    }
}
