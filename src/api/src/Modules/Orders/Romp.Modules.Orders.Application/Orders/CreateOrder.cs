using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;

namespace Romp.Modules.Orders.Application.Orders;

/// <summary>
/// Step 6 of ADR 0025: write the order, its lines, the first history row and the <c>OrderPlacedEvent</c> in one transaction of Orders alone
/// (the behaviour opens it). Everything it needs was priced by the server and checked before; it only records. A unique violation on the
/// placement key surfaces to the placement handler, which returns the twin that won. It also sets the cash on delivery timing and the staff
/// flags (first order, high value, refusal history); flags never block an order (COD-5, COD-6).
/// </summary>
public sealed record CreateOrderCommand(
    string PlacementKey,
    long? CustomerId,
    OrderContact Contact,
    OrderAddress Address,
    CheckoutPricing Priced,
    string? DiscountCode,
    short PaymentMethodId) : IRequest<CreatedOrder>, IOrdersCommand;

public sealed record CreatedOrder(long OrderId, string OrderNumber);

public sealed class CreateOrderCommandHandler(IOrdersDbContext dbContext, IOrderNumberAllocator numbers, TimeProvider clock, IOptions<OrdersOptions> options)
    : IRequestHandler<CreateOrderCommand, CreatedOrder>
{
    public async Task<CreatedOrder> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var settings = options.Value;
        var number = await numbers.NextAsync(now, cancellationToken);
        var priced = request.Priced;
        var isCod = PaymentMethods.IsCashOnDelivery(request.PaymentMethodId);

        var mobile = request.Contact.Mobile;
        var firstOrder = isCod && !await dbContext.Orders.AnyAsync(o => o.ContactMobile == mobile && o.StatusId != OrderStatuses.Cancelled, cancellationToken);
        var flagged = isCod && await dbContext.CodRisks.AnyAsync(r => r.Mobile == mobile && r.IsFlagged, cancellationToken);
        var highValue = isCod && settings.HighValueFlagAmount > 0 && priced.Total >= settings.HighValueFlagAmount;

        var order = Order.Place(new PlaceOrderData(
            number,
            request.PlacementKey,
            request.CustomerId,
            request.Contact,
            request.Address,
            [.. priced.Lines.Select(l => new OrderLineData(l.VariantId, l.ProductId, l.Sku, l.Name, l.Colour, l.Size, l.UnitPrice, l.Quantity))],
            priced.Discount,
            priced.ShippingKnown ? priced.Shipping : 0m,
            request.DiscountCode,
            request.PaymentMethodId,
            priced.CartRef,
            now)
        {
            CodFirstCallDueAt = isCod ? settings.CodPolicy.FirstCallDue(now) : null,
            IsFirstOrder = firstOrder,
            IsHighValue = highValue,
            IsRefusalFlagged = flagged,
        });

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        // The event carries the new id, so the order is saved first and then marked placed in the same transaction.
        order.MarkPlaced();

        // The payment row is written in the same transaction (Y2): owed on delivery for cash on delivery, or waiting for a gateway session with a window
        // that expires the order if it stays unpaid (PAY-11). The amount is the server-priced order total, never anything from the client (SEC-09).
        dbContext.Payments.Add(isCod
            ? Payment.ForCashOnDelivery(order.Id, order.Total)
            : Payment.ForOnline(order.Id, request.PaymentMethodId, order.Total, now.AddMinutes(settings.PaymentWindowMinutes)));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CreatedOrder(order.Id, order.OrderNumber);
    }
}
