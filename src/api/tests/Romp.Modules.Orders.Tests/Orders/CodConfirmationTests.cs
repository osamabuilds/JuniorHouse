using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class CodConfirmationTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static int _seq;

    private static string UniqueMobile() => $"03{Interlocked.Increment(ref _seq) + 200_000_000:000000000}";

    private static PlaceOrderCommand Place(string mobile, short method = PaymentMethods.CashOnDelivery, bool optIn = true) =>
        new(Guid.NewGuid().ToString("N"), new CheckoutContact("Ayesha Khan", "a@example.com", mobile, optIn),
            new CheckoutAddress("Ayesha Khan", mobile, "House 4", null, 2, 7, null, null), null, method);

    private static async Task<(string Number, string Mobile)> PlacedAsync(OrdersHost host, bool optIn = true)
    {
        var mobile = UniqueMobile();
        var result = await host.SendAsync(Place(mobile, optIn: optIn));
        return (result.OrderNumber!, "+92" + mobile[1..]);
    }

    private async Task<Order> LoadAsync(string number)
    {
        await using var db = postgres.NewContext();
        return await db.Orders.Include(o => o.History).Include(o => o.CodAttempts).SingleAsync(o => o.OrderNumber == number);
    }

    private static string StaffRef => "42";

    [Fact]
    [Trait("Spec", "COD-1")]
    public async Task ACashOnDeliveryOrder_IsPendingConfirmation_WithTheFirstCallScheduled_AndAConfirmLink()
    {
        using var host = new OrdersHost(postgres);

        var result = await host.SendAsync(Place(UniqueMobile()));

        var order = await LoadAsync(result.OrderNumber!);
        Assert.Equal(OrderStatuses.PendingConfirmation, order.StatusId);
        Assert.Equal(OrdersHost.Start.AddMinutes(60), order.CodNextDueAt);
        Assert.Equal($"tok-{result.OrderNumber}", result.ConfirmToken);
    }

    [Fact]
    [Trait("Spec", "COD-12")]
    public async Task AnOnlineOrder_HasNoConfirmLinkAndNoCallScheduled()
    {
        using var host = new OrdersHost(postgres);

        var result = await host.SendAsync(Place(UniqueMobile(), PaymentMethods.Card));

        Assert.Null(result.ConfirmToken);
        Assert.Null((await LoadAsync(result.OrderNumber!)).CodNextDueAt);
    }

    [Fact]
    [Trait("Spec", "COD-3")]
    [Trait("Spec", "COD-8")]
    public async Task ConfirmedByStaff_MakesTheOrderConfirmed_RecordsTheAttempt_AndRaisesAnEvent()
    {
        using var host = new OrdersHost(postgres);
        var (number, _) = await PlacedAsync(host);

        var result = await host.SendAsync(new RecordCodOutcomeCommand(number, CodOutcomes.Confirmed, StaffRef, "Spoke to her"));

        Assert.Equal(CodOutcomeKind.Recorded, result.Kind);
        var order = await LoadAsync(number);
        Assert.Equal(OrderStatuses.Confirmed, order.StatusId);
        var attempt = Assert.Single(order.CodAttempts);
        Assert.Equal((1, CodOutcomes.Confirmed, ActorKinds.Staff, StaffRef), (attempt.Number, attempt.OutcomeId, attempt.ActorKindId, attempt.ActorRef));
        Assert.Null(order.CodNextDueAt);
        await using var db = postgres.NewContext();
        Assert.Single(db.Set<OutboxMessage>().Where(m => m.EventType == "OrderConfirmedEvent").AsEnumerable(), m => m.Payload.Contains(number, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "COD-9")]
    public async Task ANoAnswer_SchedulesTheNextCall_AndTheLastOneStartsTheFinalWait()
    {
        using var host = new OrdersHost(postgres);
        var (number, _) = await PlacedAsync(host);

        host.Time.Now = OrdersHost.Start.AddMinutes(61);
        await host.SendAsync(new RecordCodOutcomeCommand(number, CodOutcomes.NoAnswer, StaffRef, null));
        var afterFirst = await LoadAsync(number);
        host.Time.Now = OrdersHost.Start.AddMinutes(190);
        await host.SendAsync(new RecordCodOutcomeCommand(number, CodOutcomes.NoAnswer, StaffRef, null));
        var afterSecond = await LoadAsync(number);

        Assert.Equal(OrdersHost.Start.AddMinutes(61 + 120), afterFirst.CodNextDueAt);
        Assert.Null(afterFirst.CodAutoCancelAt);
        Assert.Null(afterSecond.CodNextDueAt);
        Assert.Equal(OrdersHost.Start.AddMinutes(190).AddHours(24), afterSecond.CodAutoCancelAt);
        Assert.Equal(OrderStatuses.PendingConfirmation, afterSecond.StatusId);
    }

    [Fact]
    [Trait("Spec", "COD-2")]
    public async Task AfterTheFinalWait_TheJobCancelsTheOrder_OnceAndNotBefore()
    {
        using var host = new OrdersHost(postgres);
        var (number, _) = await PlacedAsync(host, optIn: false);
        await host.SendAsync(new RecordCodOutcomeCommand(number, CodOutcomes.NoAnswer, StaffRef, null));
        await host.SendAsync(new RecordCodOutcomeCommand(number, CodOutcomes.NoAnswer, StaffRef, null));

        host.Time.Now = OrdersHost.Start.AddHours(23);
        var early = await host.SendAsync(new RunCodConfirmationCommand());
        host.Time.Now = OrdersHost.Start.AddHours(25);
        var due = await host.SendAsync(new RunCodConfirmationCommand());
        var again = await host.SendAsync(new RunCodConfirmationCommand());

        Assert.Equal(0, early.AutoCancelled);
        Assert.True(due.AutoCancelled >= 1);
        Assert.Equal(0, again.AutoCancelled);
        var order = await LoadAsync(number);
        Assert.Equal(OrderStatuses.Cancelled, order.StatusId);
        Assert.Equal(CancelReasons.CodNoAnswer, order.History[^1].ReasonId);
        await using var db = postgres.NewContext();
        var cancelled = db.Set<OutboxMessage>().Where(m => m.EventType == "OrderCancelledEvent").AsEnumerable().Where(m => m.Payload.Contains(number, StringComparison.Ordinal)).ToList();
        Assert.Single(cancelled);
        Assert.Contains("CODN", cancelled[0].Payload, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "COD-4")]
    [Trait("Spec", "COD-5")]
    public async Task ARefusal_CancelsTheOrder_CountsAgainstTheNumber_AndFlagsItAtTheThreshold_WithoutBlockingAnOrder()
    {
        using var host = new OrdersHost(postgres);
        var mobile = UniqueMobile();
        var first = await host.SendAsync(Place(mobile));
        var second = await host.SendAsync(Place(mobile));

        await host.SendAsync(new RecordCodOutcomeCommand(first.OrderNumber!, CodOutcomes.Refused, StaffRef, null));
        await host.SendAsync(new RecordCodOutcomeCommand(second.OrderNumber!, CodOutcomes.Refused, StaffRef, null));
        var third = await host.SendAsync(Place(mobile));

        Assert.Equal(PlaceOrderOutcome.Placed, third.Outcome);
        Assert.Equal(OrderStatuses.Cancelled, (await LoadAsync(first.OrderNumber!)).StatusId);
        var placed = await LoadAsync(third.OrderNumber!);
        Assert.True(placed.IsRefusalFlagged);
        Assert.Equal(OrderStatuses.PendingConfirmation, placed.StatusId);
        await using var db = postgres.NewContext();
        var expected = "+92" + mobile[1..];
        var risk = await db.CodRisks.SingleAsync(r => r.Mobile == expected);
        Assert.Equal((2, true), (risk.RefusalCount, risk.IsFlagged));
    }

    [Fact]
    [Trait("Spec", "COD-6")]
    public async Task AFirstOrderIsFlagged_AnAnotherIsNot_AndAHighValueOneIsFlaggedWhenTheLimitIsSet()
    {
        using var host = new OrdersHost(postgres);
        host.Options.HighValueFlagAmount = 3000m;
        var mobile = UniqueMobile();

        var first = await LoadAsync((await host.SendAsync(Place(mobile))).OrderNumber!);
        var second = await LoadAsync((await host.SendAsync(Place(mobile))).OrderNumber!);

        Assert.True(first.IsFirstOrder);
        Assert.False(second.IsFirstOrder);
        Assert.True(first.IsHighValue);
    }

    [Fact]
    [Trait("Spec", "COD-12")]
    public async Task TheCustomerConfirmsWithTheLink_OnceOnly_AndALaterPressIsAlreadyHandled()
    {
        using var host = new OrdersHost(postgres);
        var placed = await host.SendAsync(Place(UniqueMobile()));

        var first = await host.SendAsync(new CustomerRespondsToCodCommand(placed.ConfirmToken, Confirm: true));
        var again = await host.SendAsync(new CustomerRespondsToCodCommand(placed.ConfirmToken, Confirm: true));
        var cancelAfter = await host.SendAsync(new CustomerRespondsToCodCommand(placed.ConfirmToken, Confirm: false));

        Assert.Equal(CustomerCodOutcome.Confirmed, first.Outcome);
        Assert.Equal(CustomerCodOutcome.AlreadyHandled, again.Outcome);
        Assert.Equal(CustomerCodOutcome.AlreadyHandled, cancelAfter.Outcome);
        var order = await LoadAsync(placed.OrderNumber!);
        Assert.Equal(OrderStatuses.Confirmed, order.StatusId);
        Assert.Equal((CodOutcomes.ConfirmedByCustomerOnPage, ActorKinds.Customer), (order.CodAttempts[0].OutcomeId, order.CodAttempts[0].ActorKindId));
        Assert.Single(order.CodAttempts);
    }

    [Fact]
    [Trait("Spec", "COD-15")]
    public async Task TheCustomerCancels_TheOrderIsCancelled_AndItIsNotARefusal()
    {
        using var host = new OrdersHost(postgres);
        var mobile = UniqueMobile();
        var placed = await host.SendAsync(Place(mobile));

        var result = await host.SendAsync(new CustomerRespondsToCodCommand(placed.ConfirmToken, Confirm: false));

        Assert.Equal(CustomerCodOutcome.Cancelled, result.Outcome);
        var order = await LoadAsync(placed.OrderNumber!);
        Assert.Equal(OrderStatuses.Cancelled, order.StatusId);
        Assert.Equal(CancelReasons.Customer, order.History[^1].ReasonId);
        await using var db = postgres.NewContext();
        var expected = "+92" + mobile[1..];
        Assert.False(await db.CodRisks.AnyAsync(r => r.Mobile == expected));
    }

    [Fact]
    [Trait("Spec", "COD-14")]
    public async Task AWhatsAppConfirm_ConfirmsOnce_AndALaterTapIsAlreadyHandled()
    {
        using var host = new OrdersHost(postgres);
        var (number, mobile) = await PlacedAsync(host);
        var id = (await LoadAsync(number)).Id;

        var first = await host.SendAsync(new CustomerRepliesOnWhatsAppCommand(id, mobile, Confirm: true));
        var again = await host.SendAsync(new CustomerRepliesOnWhatsAppCommand(id, mobile, Confirm: true));
        var cancelAfter = await host.SendAsync(new CustomerRepliesOnWhatsAppCommand(id, mobile, Confirm: false));

        Assert.Equal((CodReplyOutcome.Confirmed, CodReplyOutcome.AlreadyHandled, CodReplyOutcome.AlreadyHandled), (first, again, cancelAfter));
        var order = await LoadAsync(number);
        Assert.Equal(OrderStatuses.Confirmed, order.StatusId);
        Assert.Equal(CodOutcomes.ConfirmedByCustomerOnWhatsApp, Assert.Single(order.CodAttempts).OutcomeId);
    }

    [Fact]
    [Trait("Spec", "COD-15")]
    public async Task AWhatsAppCancel_CancelsTheOrder_AndIsNotARefusal()
    {
        using var host = new OrdersHost(postgres);
        var (number, mobile) = await PlacedAsync(host);
        var id = (await LoadAsync(number)).Id;

        var result = await host.SendAsync(new CustomerRepliesOnWhatsAppCommand(id, mobile, Confirm: false));

        Assert.Equal(CodReplyOutcome.Cancelled, result);
        Assert.Equal(OrderStatuses.Cancelled, (await LoadAsync(number)).StatusId);
        await using var db = postgres.NewContext();
        Assert.False(await db.CodRisks.AnyAsync(r => r.Mobile == mobile));
    }

    [Theory]
    [Trait("Spec", "COD-16")]
    [InlineData("+923009999999")]
    [InlineData("")]
    public async Task AWhatsAppReplyFromAnotherNumber_ChangesNothing(string sender)
    {
        using var host = new OrdersHost(postgres);
        var (number, _) = await PlacedAsync(host);
        var id = (await LoadAsync(number)).Id;

        var result = await host.SendAsync(new CustomerRepliesOnWhatsAppCommand(id, sender, Confirm: true));

        Assert.Equal(CodReplyOutcome.Ignored, result);
        Assert.Equal(OrderStatuses.PendingConfirmation, (await LoadAsync(number)).StatusId);
    }

    [Fact]
    [Trait("Spec", "COD-16")]
    public async Task AWhatsAppReplyForAnUnknownOrder_IsIgnored()
    {
        using var host = new OrdersHost(postgres);

        Assert.Equal(CodReplyOutcome.Ignored, await host.SendAsync(new CustomerRepliesOnWhatsAppCommand(long.MaxValue, "+923001234567", Confirm: true)));
    }

    [Theory]
    [Trait("Spec", "COD-19")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("tok-RMP-2026-99999-9")]
    public async Task ABadOrUnknownLink_ChangesNothing(string? token)
    {
        using var host = new OrdersHost(postgres);

        var result = await host.SendAsync(new CustomerRespondsToCodCommand(token, Confirm: true));
        var status = await host.SendAsync(new GetCodLinkStatusQuery(token));

        Assert.Equal(CustomerCodOutcome.InvalidLink, result.Outcome);
        Assert.False(status.IsValid);
    }

    [Fact]
    [Trait("Spec", "COD-12")]
    public async Task OpeningTheLink_OnlyReads_AndSaysWhetherTheOrderCanStillBeAnswered()
    {
        using var host = new OrdersHost(postgres);
        var placed = await host.SendAsync(Place(UniqueMobile()));

        var open = await host.SendAsync(new GetCodLinkStatusQuery(placed.ConfirmToken));
        Assert.True(open.CanRespond);
        Assert.Equal(OrderStatuses.PendingConfirmation, (await LoadAsync(placed.OrderNumber!)).StatusId);

        await host.SendAsync(new CustomerRespondsToCodCommand(placed.ConfirmToken, Confirm: true));
        var after = await host.SendAsync(new GetCodLinkStatusQuery(placed.ConfirmToken));
        Assert.False(after.CanRespond);
        Assert.Equal("CNFD", after.StatusCode);
    }

    [Fact]
    [Trait("Spec", "COD-13")]
    public async Task TheWhatsAppRequest_IsRaisedOnce_OnlyForAnOptedInOrder_AfterTheWait()
    {
        using var host = new OrdersHost(postgres);
        var (optedIn, _) = await PlacedAsync(host, optIn: true);
        var (notOptedIn, _) = await PlacedAsync(host, optIn: false);

        host.Time.Now = OrdersHost.Start.AddMinutes(30);
        var tooSoon = await host.SendAsync(new RunCodConfirmationCommand());
        host.Time.Now = OrdersHost.Start.AddMinutes(61);
        var due = await host.SendAsync(new RunCodConfirmationCommand());
        var again = await host.SendAsync(new RunCodConfirmationCommand());

        Assert.Equal(0, tooSoon.WhatsAppRequested);
        Assert.True(due.WhatsAppRequested >= 1);
        Assert.Equal(0, again.WhatsAppRequested);
        await using var db = postgres.NewContext();
        var requests = db.Set<OutboxMessage>().Where(m => m.EventType == "CodConfirmationRequestedEvent").AsEnumerable().ToList();
        Assert.Single(requests, m => m.Payload.Contains(optedIn, StringComparison.Ordinal));
        Assert.DoesNotContain(requests, m => m.Payload.Contains(notOptedIn, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "ORD-9")]
    public async Task CancellingAnOrder_RecordsWhoAndWhy_AndARepeatChangesNothing()
    {
        using var host = new OrdersHost(postgres);
        var (number, _) = await PlacedAsync(host);

        var first = await host.SendAsync(new CancelOrderCommand(number, CancelReasons.Staff, ActorKinds.Staff, StaffRef, "Customer called"));
        var again = await host.SendAsync(new CancelOrderCommand(number, CancelReasons.Staff, ActorKinds.Staff, StaffRef, null));
        var unknown = await host.SendAsync(new CancelOrderCommand("RMP-2026-99999-9", CancelReasons.Staff, ActorKinds.Staff, StaffRef, null));

        Assert.Equal(CancelOrderOutcome.Cancelled, first.Outcome);
        Assert.Equal(CancelOrderOutcome.AlreadyCancelled, again.Outcome);
        Assert.Equal(CancelOrderOutcome.NotFound, unknown.Outcome);
        var order = await LoadAsync(number);
        Assert.Equal((ActorKinds.Staff, StaffRef), (order.History[^1].ActorKindId, order.History[^1].ActorRef));
    }

    [Fact]
    [Trait("Spec", "ORD-10")]
    public async Task ADispatchedOrder_CannotBeCancelled_AndTheMessagePointsToReturns()
    {
        using var host = new OrdersHost(postgres);
        var (number, _) = await PlacedAsync(host);
        await using (var db = postgres.NewContext())
        {
            var order = await db.Orders.SingleAsync(o => o.OrderNumber == number);
            order.MoveTo(OrderStatuses.Confirmed, ActorKinds.Staff, "1", null, null, OrdersHost.Start);
            order.MoveTo(OrderStatuses.Dispatched, ActorKinds.System, null, null, null, OrdersHost.Start);
            await db.SaveChangesAsync();
        }

        var result = await host.SendAsync(new CancelOrderCommand(number, CancelReasons.Customer, ActorKinds.Customer, null, null));

        Assert.Equal(CancelOrderOutcome.TooLate, result.Outcome);
        Assert.Contains("return", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(OrderStatuses.Dispatched, (await LoadAsync(number)).StatusId);
    }

    [Fact]
    [Trait("Spec", "COD-3")]
    public async Task OnlyThreeOutcomesCanBeRecordedByStaff_AndOnlyOnAPendingOrder()
    {
        using var host = new OrdersHost(postgres);
        var (number, _) = await PlacedAsync(host);

        var bad = await host.SendAsync(new RecordCodOutcomeCommand(number, CodOutcomes.ConfirmedByCustomerOnWhatsApp, StaffRef, null));
        await host.SendAsync(new RecordCodOutcomeCommand(number, CodOutcomes.Confirmed, StaffRef, null));
        var late = await host.SendAsync(new RecordCodOutcomeCommand(number, CodOutcomes.NoAnswer, StaffRef, null));

        Assert.Equal(CodOutcomeKind.NotAllowedOutcome, bad.Kind);
        Assert.Equal(CodOutcomeKind.NotPending, late.Kind);
    }
}
