using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>What Shipping may read about an order to book and label a parcel (ADR 0031). One projected query: no entity leaves the module.</summary>
internal sealed class OrderShippingQueries(OrdrDbContext dbContext) : IOrderShippingQueries
{
    public async Task<OrderShippingView?> FindAsync(long orderId, CancellationToken cancellationToken)
    {
        var o = await dbContext.Orders.AsNoTracking().Where(x => x.Id == orderId)
            .Select(x => new
            {
                x.Id, x.OrderNumber, x.StatusId, x.PaymentMethodId, x.RecipientName, x.RecipientPhone, x.AddressLine1, x.AddressLine2, x.CityName, x.ProvinceName,
                x.PostalCode, x.DeliveryNote, x.Total,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (o is null)
        {
            return null;
        }

        var cod = PaymentMethods.IsCashOnDelivery(o.PaymentMethodId);
        return new OrderShippingView(
            o.Id, o.OrderNumber, OrderStatuses.CodeOf(o.StatusId), o.RecipientName, o.RecipientPhone, o.AddressLine1, o.AddressLine2, o.CityName, o.ProvinceName,
            o.PostalCode, o.DeliveryNote, cod, cod && o.StatusId != OrderStatuses.Cancelled ? o.Total : 0m);
    }
}
