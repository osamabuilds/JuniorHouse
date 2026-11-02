using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Application.OrderQueue;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class OrderQueueTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static int _seq;

    private static string UniqueMobile() => $"03{Interlocked.Increment(ref _seq) + 300_000_000:000000000}";

    private static PlaceOrderCommand Place(string mobile, short method = PaymentMethods.CashOnDelivery) =>
        new(Guid.NewGuid().ToString("N"), new CheckoutContact("Queue Tester", "q@example.com", mobile, true),
            new CheckoutAddress("Queue Tester", mobile, "House 4", null, 2, 7, null, null), null, method);

    [Fact]
    [Trait("Spec", "OQ-1")]
    public async Task TheList_FiltersByStatusAndMethod_ThenSearchesByNumberAndMobile_NewestFirst()
    {
        using var host = new OrdersHost(postgres);
        var mobile = UniqueMobile();
        var cod = await host.SendAsync(Place(mobile));
        var card = await host.SendAsync(Place(UniqueMobile(), PaymentMethods.Card));
        await host.SendAsync(new CancelOrderCommand(card.OrderNumber!, CancelReasons.Staff, ActorKinds.Staff, "1", null));

        var pending = await host.SendAsync(new ListOrdersQuery(StatusId: OrderStatuses.PendingConfirmation, Search: cod.OrderNumber));
        var byMobile = await host.SendAsync(new ListOrdersQuery(Search: mobile));
        var cancelledCard = await host.SendAsync(new ListOrdersQuery(StatusId: OrderStatuses.Cancelled, MethodId: PaymentMethods.Card, Search: card.OrderNumber));
        var wrongFilter = await host.SendAsync(new ListOrdersQuery(StatusId: OrderStatuses.Cancelled, Search: cod.OrderNumber));

        Assert.Equal(cod.OrderNumber, Assert.Single(pending.Items).OrderNumber);
        Assert.Equal(cod.OrderNumber, Assert.Single(byMobile.Items).OrderNumber);
        Assert.Equal("Cancelled", Assert.Single(cancelledCard.Items).StatusName);
        Assert.Empty(wrongFilter.Items);
    }

    [Fact]
    [Trait("Spec", "OQ-1")]
    public async Task TheList_IsPaged_NewestFirst()
    {
        using var host = new OrdersHost(postgres);
        for (var i = 0; i < 4; i++)
        {
            await host.SendAsync(Place(UniqueMobile()));
        }

        var small = await host.SendAsync(new ListOrdersQuery(Page: 1, PageSize: 2));
        var big = await host.SendAsync(new ListOrdersQuery(Page: 1, PageSize: 100));

        Assert.Equal(2, small.Items.Count);
        Assert.True(small.TotalCount >= 4);
        Assert.True(big.Items.Count >= 4);
        Assert.True(big.Items.Zip(big.Items.Skip(1)).All(p => p.First.Id > p.Second.Id));
    }

    [Fact]
    [Trait("Spec", "COD-9")]
    public async Task TheCallDueNowFilter_ListsOnlyPendingOrdersWhoseCallTimeHasCome()
    {
        using var host = new OrdersHost(postgres);
        var placed = await host.SendAsync(Place(UniqueMobile()));

        var early = await host.SendAsync(new ListOrdersQuery(CallDueNow: true, Search: placed.OrderNumber));
        host.Time.Now = OrdersHost.Start.AddMinutes(61);
        var due = await host.SendAsync(new ListOrdersQuery(CallDueNow: true, Search: placed.OrderNumber));

        Assert.Empty(early.Items);
        Assert.Single(due.Items);
    }

    [Fact]
    [Trait("Spec", "OQ-7")]
    public async Task TheDetail_ShowsContactAddressLinesHistoryAndAttempts_AndWhatStaffMayDo()
    {
        using var host = new OrdersHost(postgres);
        var placed = await host.SendAsync(Place(UniqueMobile()));
        await host.SendAsync(new RecordCodOutcomeCommand(placed.OrderNumber!, CodOutcomes.NoAnswer, "9", "Rang twice"));

        var detail = await host.SendAsync(new GetOrderQuery(placed.OrderId!.Value));

        Assert.NotNull(detail);
        Assert.Equal(("Pending confirmation", true, true, true), (detail.StatusName, detail.IsCashOnDelivery, detail.CanRecordCodOutcome, detail.CanCancel));
        Assert.Equal(2, detail.Lines.Count);
        Assert.Equal(("Placed", "Pending confirmation"), (detail.History[0].FromStatus, detail.History[0].ToStatus));
        var attempt = Assert.Single(detail.CodAttempts);
        Assert.Equal(("No answer", "Staff", "9", "Rang twice"), (attempt.Outcome, attempt.Actor, attempt.ActorRef, attempt.Note));
        Assert.Equal("Lahore", detail.CityName);
    }

    [Fact]
    [Trait("Spec", "OQ-7")]
    public async Task AnUnknownOrder_IsNull_AndACancelledOneOffersNoActions()
    {
        using var host = new OrdersHost(postgres);
        var placed = await host.SendAsync(Place(UniqueMobile()));
        await host.SendAsync(new CancelOrderCommand(placed.OrderNumber!, CancelReasons.Staff, ActorKinds.Staff, "1", null));

        Assert.Null(await host.SendAsync(new GetOrderQuery(987_654_321)));
        var detail = await host.SendAsync(new GetOrderQuery(placed.OrderId!.Value));
        Assert.False(detail!.CanCancel);
        Assert.False(detail.CanRecordCodOutcome);
    }

    [Fact]
    [Trait("Spec", "COD-10")]
    public async Task StaffClearingTheFlag_ClearsItOnTheNumberAndItsWaitingOrders_ButKeepsTheCounts()
    {
        using var host = new OrdersHost(postgres);
        var mobile = UniqueMobile();
        var a = await host.SendAsync(Place(mobile));
        var b = await host.SendAsync(Place(mobile));
        await host.SendAsync(new RecordCodOutcomeCommand(a.OrderNumber!, CodOutcomes.Refused, "1", null));
        await host.SendAsync(new RecordCodOutcomeCommand(b.OrderNumber!, CodOutcomes.Refused, "1", null));
        var third = await host.SendAsync(Place(mobile));
        Assert.True((await host.SendAsync(new GetOrderQuery(third.OrderId!.Value)))!.IsRefusalFlagged);
        var normalised = "+92" + mobile[1..];

        Assert.True(await host.SendAsync(new ClearCodFlagCommand(normalised, "7")));
        Assert.False(await host.SendAsync(new ClearCodFlagCommand("+920000000000", "7")));

        Assert.False((await host.SendAsync(new GetOrderQuery(third.OrderId!.Value)))!.IsRefusalFlagged);
        await using var db = postgres.NewContext();
        var risk = await db.CodRisks.SingleAsync(r => r.Mobile == normalised);
        Assert.Equal((false, 2, "7"), (risk.IsFlagged, risk.RefusalCount, risk.ClearedBy));
    }
}
