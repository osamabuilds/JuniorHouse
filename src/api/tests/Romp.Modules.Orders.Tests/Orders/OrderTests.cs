using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    public static PlaceOrderData Data(short method = PaymentMethods.CashOnDelivery, decimal discount = 0m, decimal shipping = 250m, string number = "RMP-2026-00001-4") =>
        new(
            number,
            "key-1234567890abcdef",
            null,
            new OrderContact("Ayesha Khan", "ayesha@example.com", "+923001234567", true),
            new OrderAddress("Ayesha Khan", "+923001234567", "House 4, Street 2", null, 7, "Lahore", 2, "Punjab", "54000", "Ring the bell"),
            [new OrderLineData(11, 1, "ROMP-S11", "Floral Dress", "Red", "2-3Y", 1200m, 2), new OrderLineData(12, 2, "ROMP-S12", "Tee", "Blue", "4-5Y", 500m, 1)],
            discount,
            shipping,
            discount > 0 ? "WELCOME10" : null,
            method,
            "42",
            Now);

    [Fact]
    [Trait("Spec", "ORD-1")]
    public void ACashOnDeliveryOrder_StartsPendingConfirmation_WithTotalsFromTheLines()
    {
        var order = Order.Place(Data(discount: 290m));

        Assert.Equal(OrderStatuses.PendingConfirmation, order.StatusId);
        Assert.Equal(2900m, order.Subtotal);
        Assert.Equal(290m, order.Discount);
        Assert.Equal(250m, order.Shipping);
        Assert.Equal(2860m, order.Total);
        Assert.Equal(2400m, order.Lines[0].LineTotal);
    }

    [Theory]
    [Trait("Spec", "ORD-1")]
    [InlineData(PaymentMethods.Easypaisa)]
    [InlineData(PaymentMethods.JazzCash)]
    [InlineData(PaymentMethods.Card)]
    public void AnOnlineOrder_StartsPaymentPending(short method)
    {
        Assert.Equal(OrderStatuses.PaymentPending, Order.Place(Data(method)).StatusId);
    }

    [Fact]
    [Trait("Spec", "ORD-6")]
    public void ThePlacedOrder_KeepsSnapshotsOfContactAddressAndLines()
    {
        var order = Order.Place(Data());

        Assert.Equal(("Ayesha Khan", "+923001234567", true), (order.ContactName, order.ContactMobile, order.WhatsAppOptIn));
        Assert.Equal(Now, order.WhatsAppOptInAt);
        Assert.Equal(("Lahore", "Punjab", "Ring the bell"), (order.CityName, order.ProvinceName, order.DeliveryNote));
        Assert.Equal(["ROMP-S11", "ROMP-S12"], order.Lines.Select(l => l.Sku));
    }

    [Fact]
    [Trait("Spec", "ORD-1")]
    public void MarkingPlaced_WritesTheFirstHistoryRow_AndRaisesOneEventWithNoPersonalData()
    {
        var order = Order.Place(Data());

        order.MarkPlaced();
        order.MarkPlaced();

        var row = Assert.Single(order.History);
        Assert.Equal((null, OrderStatuses.PendingConfirmation), (row.FromStatusId, row.ToStatusId));
        var raised = Assert.IsType<OrderPlacedEvent>(Assert.Single(order.DomainEvents));
        Assert.Equal(("RMP-2026-00001-4", "COD", "42"), (raised.OrderNumber, raised.PaymentMethodCode, raised.CartRef));
    }

    [Fact]
    [Trait("Spec", "SEC-09")]
    public void ADiscountLargerThanTheSubtotal_OrABadLine_IsRefused()
    {
        Assert.Throws<DomainException>(() => Order.Place(Data(discount: 2901m)));
        Assert.Throws<DomainException>(() => Order.Place(Data() with { Lines = [new OrderLineData(1, 1, null, "X", "R", "S", 0m, 1)] }));
        Assert.Throws<DomainException>(() => Order.Place(Data() with { Lines = [] }));
        Assert.Throws<DomainException>(() => Order.Place(Data() with { Shipping = -1m }));
    }

    [Fact]
    [Trait("Spec", "ORD-5")]
    public void ABadNumberKeyOrMethod_IsRefused()
    {
        Assert.Throws<DomainException>(() => Order.Place(Data(number: "RMP-2026-00001-1")));
        Assert.Throws<DomainException>(() => Order.Place(Data() with { PlacementKey = " " }));
        Assert.Throws<DomainException>(() => Order.Place(Data(method: 9)));
    }

    [Fact]
    [Trait("Spec", "ORD-9")]
    public void Cancelling_MovesToCancelled_RecordsWhoAndWhy_RaisesOneEvent_AndRepeatsChangeNothing()
    {
        var order = Order.Place(Data());
        order.MarkPlaced();

        var first = order.Cancel(CancelReasons.Customer, ActorKinds.Customer, null, "changed my mind", false, Now.AddMinutes(5));
        var again = order.Cancel(CancelReasons.Customer, ActorKinds.Customer, null, null, false, Now.AddMinutes(6));

        Assert.Equal(OrderTransition.Changed, first);
        Assert.Equal(OrderTransition.Unchanged, again);
        Assert.Equal(OrderStatuses.Cancelled, order.StatusId);
        Assert.Equal(2, order.History.Count);
        Assert.Equal((CancelReasons.Customer, ActorKinds.Customer), (order.History[1].ReasonId, order.History[1].ActorKindId));
        var cancelled = Assert.Single(order.DomainEvents.OfType<OrderCancelledEvent>());
        Assert.Equal(("CUST", false), (cancelled.ReasonCode, cancelled.WasPicked));
    }

    [Fact]
    [Trait("Spec", "ORD-10")]
    public void ADispatchedOrder_CannotBeCancelled()
    {
        var order = Order.Place(Data());
        order.MarkPlaced();
        order.MoveTo(OrderStatuses.Confirmed, ActorKinds.Staff, "7", null, null, Now);
        order.MoveTo(OrderStatuses.Dispatched, ActorKinds.System, null, null, null, Now);

        var result = order.Cancel(CancelReasons.Staff, ActorKinds.Staff, "7", null, true, Now);

        Assert.Equal(OrderTransition.NotAllowed, result);
        Assert.Equal(OrderStatuses.Dispatched, order.StatusId);
        Assert.DoesNotContain(order.DomainEvents, e => e is OrderCancelledEvent);
    }

    [Fact]
    [Trait("Spec", "ORD-5")]
    public void ALongNote_IsCutToTheLimit()
    {
        var order = Order.Place(Data());
        order.MarkPlaced();
        order.MoveTo(OrderStatuses.Confirmed, ActorKinds.Staff, "1", null, new string('x', 500), Now);

        Assert.Equal(OrderHistory.MaxNoteLength, order.History[^1].Note!.Length);
    }
}
