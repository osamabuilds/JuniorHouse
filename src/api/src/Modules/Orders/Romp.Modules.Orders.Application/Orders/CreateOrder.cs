using MediatR;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Application.Orders;

/// <summary>
/// Step 6 of ADR 0025: write the order, its lines, the first history row and the <c>OrderPlacedEvent</c> in one transaction of Orders alone
/// (the behaviour opens it). Everything it needs was priced by the server and checked before; it only records. A unique violation on the
/// placement key surfaces to the placement handler, which returns the twin that won.
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

public sealed class CreateOrderCommandHandler(IOrdersDbContext dbContext, IOrderNumberAllocator numbers, TimeProvider clock) : IRequestHandler<CreateOrderCommand, CreatedOrder>
{
    public async Task<CreatedOrder> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var number = await numbers.NextAsync(now, cancellationToken);
        var priced = request.Priced;
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
            now));

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        // The event carries the new id, so the order is saved first and then marked placed in the same transaction.
        order.MarkPlaced();
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CreatedOrder(order.Id, order.OrderNumber);
    }
}
