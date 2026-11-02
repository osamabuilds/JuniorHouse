using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class OrderStatusTransitionTests
{
    private static readonly short[] All =
    [
        OrderStatuses.PendingConfirmation, OrderStatuses.Confirmed, OrderStatuses.PaymentPending, OrderStatuses.Paid,
        OrderStatuses.Cancelled, OrderStatuses.Dispatched, OrderStatuses.Delivered,
    ];

    // The one table the specification allows (Q-ORD-4). Anything not listed must be refused.
    private static readonly HashSet<(short, short)> Allowed =
    [
        (OrderStatuses.PendingConfirmation, OrderStatuses.Confirmed),
        (OrderStatuses.PendingConfirmation, OrderStatuses.Cancelled),
        (OrderStatuses.PaymentPending, OrderStatuses.Paid),
        (OrderStatuses.PaymentPending, OrderStatuses.Cancelled),
        (OrderStatuses.Confirmed, OrderStatuses.Cancelled),
        (OrderStatuses.Confirmed, OrderStatuses.Dispatched),
        (OrderStatuses.Paid, OrderStatuses.Cancelled),
        (OrderStatuses.Paid, OrderStatuses.Dispatched),
        (OrderStatuses.Dispatched, OrderStatuses.Delivered),
    ];

    [Fact]
    [Trait("Spec", "ORD-5")]
    public void EveryPairOfStates_IsAllowedExactlyWhenTheTableSaysSo()
    {
        foreach (var from in All)
        {
            foreach (var to in All)
            {
                Assert.True(Allowed.Contains((from, to)) == OrderTransitions.IsAllowed(from, to), $"{OrderStatuses.CodeOf(from)} to {OrderStatuses.CodeOf(to)}");
            }
        }
    }

    [Fact]
    [Trait("Spec", "ORD-5")]
    public void CancelledAndDeliveredAreEndStates()
    {
        Assert.Empty(OrderTransitions.From(OrderStatuses.Cancelled));
        Assert.Empty(OrderTransitions.From(OrderStatuses.Delivered));
    }

    [Fact]
    [Trait("Spec", "ORD-10")]
    public void NothingMovesBackwards_AndNoPaymentStateJumpsToAnother()
    {
        Assert.False(OrderTransitions.IsAllowed(OrderStatuses.Dispatched, OrderStatuses.Confirmed));
        Assert.False(OrderTransitions.IsAllowed(OrderStatuses.Paid, OrderStatuses.PaymentPending));
        Assert.False(OrderTransitions.IsAllowed(OrderStatuses.PendingConfirmation, OrderStatuses.Paid));
        Assert.False(OrderTransitions.IsAllowed(OrderStatuses.Cancelled, OrderStatuses.Confirmed));
    }

    [Fact]
    [Trait("Spec", "ORD-5")]
    public void EveryStatusHasAStableCode()
    {
        Assert.Equal(["PNDC", "CNFD", "PYPN", "PAID", "CNCL", "DSPT", "DLVD"], All.OrderBy(i => i).Select(OrderStatuses.CodeOf));
    }
}
