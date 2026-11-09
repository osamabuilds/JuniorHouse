using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;
using Romp.Modules.Orders.Infrastructure.Payments;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Payments;

/// <summary>
/// The online payment flow end to end against a real PostgreSQL, with only the gateway faked: place, open the payment page, signed callbacks,
/// declines and retries, lost callbacks, expiry, circuit breaker (orders payments Y1 to Y9). Time is a clock the test moves.
/// </summary>
public sealed class PaymentFlowTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private const decimal Total = 3150m;

    // ---- Y1, Y2: the payment row is written with the order -------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "FR-27")]
    [Trait("Spec", "SEC-09")]
    public async Task AnOnlineOrder_GetsAnInitiatedPayment_ForTheServerPricedTotal_WithAWindow()
    {
        using var rig = new PaymentRig(postgres);

        var placed = await rig.PlaceOnlineAsync();

        var order = await rig.OrderAsync(placed.OrderNumber);
        var payment = await rig.PaymentAsync(placed.OrderNumber);
        Assert.Equal(OrderStatuses.PaymentPending, order.StatusId);
        Assert.Equal((PaymentStatuses.Initiated, PaymentMethods.Card, Total), (payment.StatusId, payment.MethodId, payment.Amount));
        Assert.Equal(order.Total, payment.Amount);
        Assert.Equal(OrdersHost.Start.AddMinutes(30), payment.WindowEndsAt);
        Assert.Empty(payment.Attempts);
    }

    [Fact]
    [Trait("Spec", "FR-27")]
    public async Task ACashOnDeliveryOrder_GetsAPendingPaymentRow_AndNoPayToken()
    {
        using var rig = new PaymentRig(postgres);

        var result = await rig.Host.SendAsync(PaymentRig.Place(PaymentMethods.CashOnDelivery));

        var payment = await rig.PaymentAsync(result.OrderNumber!);
        Assert.Equal((PaymentStatuses.Pending, PaymentMethods.CashOnDelivery, Total), (payment.StatusId, payment.MethodId, payment.Amount));
        Assert.Null(payment.WindowEndsAt);
        Assert.Null(result.PayToken);
        Assert.NotNull(result.ConfirmToken);
    }

    [Fact]
    [Trait("Spec", "PAY-3")]
    public async Task ARepeatedPlacement_GivesTheSameOrder_AndStillOnePayment()
    {
        using var rig = new PaymentRig(postgres);
        var command = PaymentRig.Place();

        var first = await rig.Host.SendAsync(command);
        var second = await rig.Host.SendAsync(command);

        Assert.True(second.WasRepeat);
        Assert.Equal(first.OrderNumber, second.OrderNumber);
        Assert.Equal(first.PayToken, second.PayToken);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Payments.CountAsync(p => db.Orders.Any(o => o.Id == p.OrderId && o.OrderNumber == first.OrderNumber)));
    }

    // ---- Y3: opening the payment page -----------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-10")]
    public async Task StartingAPayment_OpensASession_AndRecordsThePendingAttempt()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceOnlineAsync();

        var started = await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));

        Assert.Equal(StartPaymentKind.Started, started.Kind);
        Assert.StartsWith("/customer-api/orders/payments/fake/fk_", started.RedirectUrl, StringComparison.Ordinal);
        var payment = await rig.PaymentAsync(placed.OrderNumber);
        Assert.Equal(PaymentStatuses.Pending, payment.StatusId);
        var attempt = Assert.Single(payment.Attempts);
        Assert.Equal((1, PaymentStatuses.Pending), (attempt.AttemptNo, attempt.StatusId));
        var session = Assert.Single(rig.Gateway.Sessions);
        Assert.Equal((Total, "CARD", placed.OrderNumber), (session.Amount, session.MethodCode, session.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "PAY-3")]
    public async Task PressingPayTwice_ReturnsTheSamePage_AndNeverOpensASecondSession()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceOnlineAsync();

        var first = await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));
        var second = await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));

        Assert.Equal(StartPaymentKind.Started, first.Kind);
        Assert.Equal(StartPaymentKind.Reused, second.Kind);
        Assert.Equal(first.RedirectUrl, second.RedirectUrl);
        Assert.Equal(1, rig.Gateway.SessionCreateCalls);
        Assert.Single((await rig.PaymentAsync(placed.OrderNumber)).Attempts);
    }

    [Fact]
    [Trait("Spec", "PAY-3")]
    public async Task WhenTheGatewayDoesNotAnswer_NothingIsRecorded_AndARetryReachesTheSameSession()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceOnlineAsync();
        rig.Gateway.Mode = FakeGatewayMode.Down;

        var down = await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));

        Assert.Equal(StartPaymentKind.GatewayUnavailable, down.Kind);
        Assert.Empty((await rig.PaymentAsync(placed.OrderNumber)).Attempts);
        Assert.Equal((OrderStatuses.PaymentPending, PaymentStatuses.Initiated), ((await rig.OrderAsync(placed.OrderNumber)).StatusId, (await rig.PaymentAsync(placed.OrderNumber)).StatusId));

        rig.Gateway.Mode = FakeGatewayMode.Normal;
        var retry = await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));
        Assert.Equal(StartPaymentKind.Started, retry.Kind);
        Assert.Single(rig.Gateway.Sessions);
    }

    [Theory]
    [Trait("Spec", "SEC-05")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("pay-RMP-2026-99999-9")]
    public async Task StartingWithABadOrUnknownToken_IsAnInvalidLink_AndDoesNothing(string? token)
    {
        using var rig = new PaymentRig(postgres);

        var result = await rig.Host.SendAsync(new StartOnlinePaymentCommand(token));

        Assert.Equal(StartPaymentKind.InvalidLink, result.Kind);
        Assert.Equal(0, rig.Gateway.SessionCreateCalls);
    }

    [Fact]
    public async Task ACashOnDeliveryOrder_CannotStartAnOnlinePayment()
    {
        using var rig = new PaymentRig(postgres);
        var cod = await rig.Host.SendAsync(PaymentRig.Place(PaymentMethods.CashOnDelivery));

        var result = await rig.Host.SendAsync(new StartOnlinePaymentCommand($"pay-{cod.OrderNumber}"));

        Assert.Equal(StartPaymentKind.NotOnline, result.Kind);
        Assert.Equal(0, rig.Gateway.SessionCreateCalls);
    }

    [Fact]
    [Trait("Spec", "PAY-7")]
    public async Task WhileTheCircuitIsOpen_NoSessionIsRequested_AndTheShopperIsToldToUseCashOnDelivery()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceOnlineAsync();
        for (var i = 0; i < rig.Host.Options.CircuitFailureThreshold; i++)
        {
            rig.Host.Circuit.RecordFailure();
        }

        var result = await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));

        Assert.Equal(StartPaymentKind.GatewayUnavailable, result.Kind);
        Assert.Equal(0, rig.Gateway.SessionCreateCalls);
        Assert.False(rig.Host.Resilient.IsAvailable);
        Assert.True(rig.Host.Resilient.IsPaused);
    }

    // ---- Y4: the signed webhook -------------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-1")]
    [Trait("Spec", "TC-CHK-11")]
    public async Task ASignedSuccess_ConfirmsThePayment_MarksTheOrderPaid_AndRaisesOnePaidEvent()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();

        var result = await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid);

        Assert.Equal(WebhookResultKind.Accepted, result.Kind);
        var order = await rig.OrderAsync(placed.OrderNumber);
        var payment = await rig.PaymentAsync(placed.OrderNumber);
        Assert.Equal(OrderStatuses.Paid, order.StatusId);
        Assert.Equal((PaymentStatuses.Paid, null), (payment.StatusId, payment.ReviewReasonId));
        Assert.NotNull(payment.PaidAt);
        Assert.Equal(PaymentStatuses.Paid, payment.Attempts.Single().StatusId);
        Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        Assert.Equal(ActorKinds.Gateway, order.History[^1].ActorKindId);
        var hook = Assert.Single(await rig.WebhooksAsync(payment.Id));
        Assert.Equal((true, WebhookOutcomes.Applied), (hook.SignatureVerified, hook.OutcomeId));
    }

    [Fact]
    [Trait("Spec", "PAY-5")]
    public async Task TheSameEventDeliveredTwice_ChangesTheOrderOnce()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        var (body, headers) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid, eventId: "evt-same");

        var first = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, headers));
        var second = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, headers));

        Assert.Equal(WebhookResultKind.Accepted, first.Kind);
        Assert.Equal(WebhookResultKind.Duplicate, second.Kind);
        Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        Assert.Single(await rig.WebhooksAsync((await rig.PaymentAsync(placed.OrderNumber)).Id));
    }

    [Fact]
    [Trait("Spec", "PAY-5")]
    public async Task ASuccessResentUnderANewEventId_StillChangesTheOrderOnce()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();

        await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid);
        var resent = await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid);

        Assert.Equal(WebhookResultKind.Accepted, resent.Kind);
        Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        var hooks = await rig.WebhooksAsync((await rig.PaymentAsync(placed.OrderNumber)).Id);
        Assert.Equal([WebhookOutcomes.Applied, WebhookOutcomes.Ignored], hooks.OrderBy(h => h.Id).Select(h => h.OutcomeId));
    }

    [Fact]
    [Trait("Spec", "PAY-4")]
    public async Task ACallbackWithABadSignature_IsRejected_StoredAsUnverified_AndChangesNothing()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        var (body, _) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid);
        var forged = new Dictionary<string, string> { [FakeGateway.SignatureHeader] = $"t={OrdersHost.Start.ToUnixTimeSeconds()},v1={new string('a', 64)}" };

        var result = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, forged));

        Assert.Equal((WebhookResultKind.Rejected, CallbackRejection.BadSignature), (result.Kind, result.Rejection));
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(PaymentStatuses.Pending, (await rig.PaymentAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(0, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        await using var db = postgres.NewContext();
        var rejected = await db.PaymentWebhooks.AsNoTracking().Where(w => w.OutcomeId == WebhookOutcomes.Rejected && !w.SignatureVerified).OrderByDescending(w => w.Id).FirstAsync();
        Assert.Equal((false, null, null), (rejected.SignatureVerified, rejected.EventId, rejected.PaymentId));
        Assert.Equal(PaymentWebhook.HashLength, rejected.BodyHash.Length);
    }

    [Fact]
    [Trait("Spec", "PAY-4")]
    public async Task ACallbackWithNoSignature_IsRejected()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        var (body, _) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid);

        var result = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, new Dictionary<string, string>()));

        Assert.Equal((WebhookResultKind.Rejected, CallbackRejection.MissingSignature), (result.Kind, result.Rejection));
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
    }

    [Fact]
    [Trait("Spec", "PAY-12")]
    public async Task ASignedCallbackReplayedLater_IsRejected()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        var (body, headers) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid);
        rig.Advance(TimeSpan.FromMinutes(10));

        var result = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, headers));

        Assert.Equal((WebhookResultKind.Rejected, CallbackRejection.StaleTimestamp), (result.Kind, result.Rejection));
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
    }

    [Fact]
    [Trait("Spec", "SEC-10")]
    public async Task AWebhookForAProviderThatIsNotSwitchedOn_IsRefused_AndNothingIsStored()
    {
        using var rig = new PaymentRig(postgres);
        await using var before = postgres.NewContext();
        var count = await before.PaymentWebhooks.CountAsync();
        var (body, headers) = rig.Gateway.BuildCallback("fk_x", GatewayOutcome.Paid);

        var result = await rig.Host.SendAsync(new HandlePaymentWebhookCommand("evil-provider", body, headers));

        Assert.Equal(WebhookResultKind.UnknownProvider, result.Kind);
        await using var after = postgres.NewContext();
        Assert.Equal(count, await after.PaymentWebhooks.CountAsync());
    }

    [Fact]
    [Trait("Spec", "PAY-8")]
    public async Task AnAmountThatIsNotTheOrderTotal_IsFlaggedForReview_AndTheOrderIsNotConfirmed()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();

        var result = await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid, amount: 1m);

        Assert.Equal(WebhookResultKind.Accepted, result.Kind);
        var payment = await rig.PaymentAsync(placed.OrderNumber);
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal((PaymentStatuses.InReview, PaymentReviewReasons.AmountMismatch), (payment.StatusId, payment.ReviewReasonId));
        Assert.Equal(0, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        Assert.Equal(1, await rig.EventCountAsync("OrderPaymentSuspectEvent", placed.OrderNumber));
        Assert.Equal(WebhookOutcomes.Flagged, Assert.Single(await rig.WebhooksAsync(payment.Id)).OutcomeId);
    }

    [Fact]
    public async Task ASignedMessageAboutAnAttemptWeNeverStarted_IsKeptForTheRecord_AndChangesNothing()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();

        var result = await rig.DeliverAsync("fk_unknown_reference", GatewayOutcome.Paid, amount: Total, eventId: "evt-unmatched-1");

        Assert.Equal(WebhookResultKind.Accepted, result.Kind);
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        await using var db = postgres.NewContext();
        var hook = await db.PaymentWebhooks.AsNoTracking().SingleAsync(w => w.EventId == "evt-unmatched-1");
        Assert.Equal((WebhookOutcomes.Unmatched, null), (hook.OutcomeId, hook.PaymentId));
    }

    [Fact]
    [Trait("Spec", "PAY-9")]
    public async Task OnlyAHashOfTheBodyIsKept_NeverThePayload()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        var (body, headers) = rig.Gateway.BuildCallback(placed.GatewayRef!, GatewayOutcome.Paid, eventId: "evt-hash");
        await rig.Host.SendAsync(new HandlePaymentWebhookCommand("fake", body, headers));

        await using var db = postgres.NewContext();
        var hook = await db.PaymentWebhooks.AsNoTracking().SingleAsync(w => w.EventId == "evt-hash");

        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(body)).ToLowerInvariant(), hook.BodyHash);
        var columns = db.Model.FindEntityType(typeof(PaymentWebhook))!.GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(columns, c => c.Contains("Body", StringComparison.Ordinal) && c != nameof(PaymentWebhook.BodyHash));
        Assert.DoesNotContain(columns, c => c.Contains("Payload", StringComparison.OrdinalIgnoreCase) || c.Contains("Card", StringComparison.OrdinalIgnoreCase));
    }

    // ---- Y5: decline and retry -------------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-2")]
    [Trait("Spec", "TC-CHK-12")]
    public async Task ADecline_KeepsTheOrder_ShowsFailed_AndAllowsARetryThatCanSucceed()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();

        await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Failed, amount: 0m);

        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(PaymentStatuses.Failed, (await rig.PaymentAsync(placed.OrderNumber)).StatusId);
        var status = await rig.Host.SendAsync(new GetPaymentStatusQuery(placed.Token));
        Assert.Equal(("failed", true), (status!.State, status.CanRetry));

        var retry = await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));
        Assert.Equal(StartPaymentKind.Started, retry.Kind);
        var second = await rig.OpenRefAsync(placed.OrderNumber);
        Assert.NotEqual(placed.GatewayRef, second);
        await rig.DeliverAsync(second!, GatewayOutcome.Paid);

        Assert.Equal(OrderStatuses.Paid, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "PAY-3")]
    public async Task AfterTheLastDecline_TheOrderIsCancelledAsPaymentFailed_AndStockIsGivenBack()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceOnlineAsync();
        var max = rig.Host.Options.PaymentMaxAttempts;

        for (var i = 1; i <= max; i++)
        {
            var started = await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));
            Assert.Equal(StartPaymentKind.Started, started.Kind);
            await rig.DeliverAsync((await rig.OpenRefAsync(placed.OrderNumber))!, GatewayOutcome.Failed, amount: 0m);
        }

        var order = await rig.OrderAsync(placed.OrderNumber);
        Assert.Equal(OrderStatuses.Cancelled, order.StatusId);
        Assert.Equal(CancelReasons.PaymentFailed, order.History[^1].ReasonId);
        Assert.Equal(1, await rig.EventCountAsync("OrderCancelledEvent", placed.OrderNumber));
        var again = await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));
        Assert.Equal(StartPaymentKind.OrderClosed, again.Kind);
    }

    [Fact]
    [Trait("Spec", "PAY-3")]
    public async Task ALateSuccessOnAnOldAttempt_AfterAnotherAlreadyPaid_IsFlaggedAsADuplicateCharge()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceOnlineAsync();
        await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));
        var first = (await rig.OpenRefAsync(placed.OrderNumber))!;
        await rig.DeliverAsync(first, GatewayOutcome.Failed, amount: 0m);
        await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));
        var second = (await rig.OpenRefAsync(placed.OrderNumber))!;
        await rig.DeliverAsync(second, GatewayOutcome.Paid);

        await rig.DeliverAsync(first, GatewayOutcome.Paid);

        var payment = await rig.PaymentAsync(placed.OrderNumber);
        Assert.Equal(OrderStatuses.Paid, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal((PaymentStatuses.Paid, PaymentReviewReasons.DuplicateCharge), (payment.StatusId, payment.ReviewReasonId));
        Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        Assert.Equal(1, await rig.EventCountAsync("OrderPaymentSuspectEvent", placed.OrderNumber));
    }

    [Fact]
    public async Task APaidOrder_CannotStartAnotherPayment()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid);

        var again = await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));

        Assert.Equal(StartPaymentKind.AlreadyPaid, again.Kind);
        Assert.Equal(1, rig.Gateway.SessionCreateCalls);
    }

    // ---- Y6: reconciliation (a lost callback) ------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-6")]
    [Trait("Spec", "FR-30")]
    public async Task ALostCallback_IsFoundByReconciliation_AndTheOrderIsCompletedExactlyOnce()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        rig.Gateway.SetOutcome(placed.GatewayRef!, GatewayOutcome.Paid);
        rig.Advance(TimeSpan.FromMinutes(11));

        var first = await rig.Host.SendAsync(new ReconcilePaymentsCommand());
        var second = await rig.Host.SendAsync(new ReconcilePaymentsCommand());

        Assert.True(first.Settled >= 1);
        Assert.Equal(0, second.Settled);
        Assert.Equal(OrderStatuses.Paid, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(PaymentStatuses.Paid, (await rig.PaymentAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "PAY-6")]
    public async Task ReconciliationLeavesRecentAttemptsAlone_ButTheLateWebhookStillWorks()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        rig.Gateway.SetOutcome(placed.GatewayRef!, GatewayOutcome.Paid);
        rig.Advance(TimeSpan.FromMinutes(9));

        await rig.Host.SendAsync(new ReconcilePaymentsCommand());
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);

        await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid);
        Assert.Equal(OrderStatuses.Paid, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
    }

    [Fact]
    [Trait("Spec", "PAY-6")]
    public async Task ReconciliationThenTheLateWebhook_StillCountsTheMoneyOnce()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        rig.Gateway.SetOutcome(placed.GatewayRef!, GatewayOutcome.Paid);
        rig.Advance(TimeSpan.FromMinutes(11));
        await rig.Host.SendAsync(new ReconcilePaymentsCommand());

        var late = await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid);

        Assert.Equal(WebhookResultKind.Accepted, late.Kind);
        Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        Assert.Null((await rig.PaymentAsync(placed.OrderNumber)).ReviewReasonId);
    }

    [Fact]
    [Trait("Spec", "PAY-6")]
    public async Task WhenTheGatewayCannotBeAsked_ReconciliationStopsAndChangesNothing()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        rig.Gateway.SetOutcome(placed.GatewayRef!, GatewayOutcome.Paid);
        rig.Advance(TimeSpan.FromMinutes(11));
        rig.Gateway.Mode = FakeGatewayMode.Down;

        var result = await rig.Host.SendAsync(new ReconcilePaymentsCommand());

        Assert.True(result.GatewayUnreachable);
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
    }

    [Fact]
    [Trait("Spec", "PAY-6")]
    public async Task ReconciliationRecordsADeclineTheGatewayReports_AndLeavesAPendingOneAlone()
    {
        using var rig = new PaymentRig(postgres);
        var declined = await rig.PlaceAndStartAsync();
        var waiting = await rig.PlaceAndStartAsync();
        rig.Gateway.SetOutcome(declined.GatewayRef!, GatewayOutcome.Failed);
        rig.Advance(TimeSpan.FromMinutes(11));

        await rig.Host.SendAsync(new ReconcilePaymentsCommand());

        Assert.Equal(PaymentStatuses.Failed, (await rig.PaymentAsync(declined.OrderNumber)).StatusId);
        Assert.Equal(PaymentStatuses.Pending, (await rig.PaymentAsync(waiting.OrderNumber)).StatusId);
    }

    [Fact]
    [Trait("Spec", "PAY-8")]
    public async Task ReconciliationThatSeesTheWrongAmount_HoldsItForReview()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        rig.Gateway.SetOutcome(placed.GatewayRef!, GatewayOutcome.Paid);
        // The payment row is changed to a different amount than the gateway reports as charged, as a wrong amount would look.
        await using (var db = postgres.NewContext())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE \"ORDR\".\"PAYM_MAIN\" SET \"AMT\" = 3000 WHERE \"ID\" = {0}", (await rig.PaymentAsync(placed.OrderNumber)).Id);
        }

        rig.Advance(TimeSpan.FromMinutes(11));
        await rig.Host.SendAsync(new ReconcilePaymentsCommand());

        var payment = await rig.PaymentAsync(placed.OrderNumber);
        Assert.Equal((PaymentStatuses.InReview, PaymentReviewReasons.AmountMismatch), (payment.StatusId, payment.ReviewReasonId));
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
    }

    // ---- Y8: expiry --------------------------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-11")]
    public async Task AnUnpaidOnlineOrder_ExpiresWhenTheWindowCloses_AndIsCancelledAsPaymentExpired()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        rig.Advance(TimeSpan.FromMinutes(29));

        var early = await rig.Host.SendAsync(new ExpireUnpaidOrdersCommand());
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);

        rig.Advance(TimeSpan.FromMinutes(2));
        var result = await rig.Host.SendAsync(new ExpireUnpaidOrdersCommand());

        Assert.True(result.Expired >= 1);
        Assert.Equal(0, early.Expired);
        var order = await rig.OrderAsync(placed.OrderNumber);
        var payment = await rig.PaymentAsync(placed.OrderNumber);
        Assert.Equal(OrderStatuses.Cancelled, order.StatusId);
        Assert.Equal(CancelReasons.PaymentExpired, order.History[^1].ReasonId);
        Assert.Equal(PaymentStatuses.Expired, payment.StatusId);
        Assert.Equal(PaymentStatuses.Expired, payment.Attempts.Single().StatusId);
        Assert.Equal(1, await rig.EventCountAsync("OrderCancelledEvent", placed.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "PAY-11")]
    public async Task AnOrderThatNeverStartedPaying_AlsoExpires()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceOnlineAsync();
        rig.Advance(TimeSpan.FromMinutes(31));

        await rig.Host.SendAsync(new ExpireUnpaidOrdersCommand());

        Assert.Equal(OrderStatuses.Cancelled, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
    }

    [Fact]
    [Trait("Spec", "FR-30")]
    public async Task APaidOrderIsNeverExpired_EvenIfTheCallbackWasLost()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        rig.Gateway.SetOutcome(placed.GatewayRef!, GatewayOutcome.Paid);
        rig.Advance(TimeSpan.FromMinutes(31));

        var result = await rig.Host.SendAsync(new ExpireUnpaidOrdersCommand());

        Assert.True(result.SettledInstead >= 1);
        Assert.Equal(OrderStatuses.Paid, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal(0, await rig.EventCountAsync("OrderCancelledEvent", placed.OrderNumber));
        Assert.Equal(1, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "FR-30")]
    public async Task WhenTheGatewayCannotBeAsked_AnOrderWithAnOpenAttemptIsNotCancelledOnAGuess()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        rig.Advance(TimeSpan.FromMinutes(31));
        rig.Gateway.Mode = FakeGatewayMode.Down;

        var result = await rig.Host.SendAsync(new ExpireUnpaidOrdersCommand());

        Assert.True(result.Skipped >= 1);
        Assert.Equal(OrderStatuses.PaymentPending, (await rig.OrderAsync(placed.OrderNumber)).StatusId);

        rig.Gateway.Mode = FakeGatewayMode.Normal;
        await rig.Host.SendAsync(new ExpireUnpaidOrdersCommand());
        Assert.Equal(OrderStatuses.Cancelled, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
    }

    [Fact]
    [Trait("Spec", "PAY-11")]
    public async Task MoneyThatArrivesAfterTheOrderExpired_IsHeldForReview_AndTheOrderStaysCancelled()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();
        rig.Advance(TimeSpan.FromMinutes(31));
        await rig.Host.SendAsync(new ExpireUnpaidOrdersCommand());

        await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid, at: rig.Host.Time.Now);

        var payment = await rig.PaymentAsync(placed.OrderNumber);
        Assert.Equal(OrderStatuses.Cancelled, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
        Assert.Equal((PaymentStatuses.InReview, PaymentReviewReasons.OrderNotPayable), (payment.StatusId, payment.ReviewReasonId));
        Assert.Equal(0, await rig.EventCountAsync("OrderPaidEvent", placed.OrderNumber));
        Assert.Equal(1, await rig.EventCountAsync("OrderPaymentSuspectEvent", placed.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "PAY-11")]
    public async Task ACashOnDeliveryOrder_NeverExpiresThroughThePaymentWindow()
    {
        using var rig = new PaymentRig(postgres);
        var cod = await rig.Host.SendAsync(PaymentRig.Place(PaymentMethods.CashOnDelivery));
        rig.Advance(TimeSpan.FromHours(5));

        await rig.Host.SendAsync(new ExpireUnpaidOrdersCommand());

        Assert.Equal(OrderStatuses.PendingConfirmation, (await rig.OrderAsync(cod.OrderNumber!)).StatusId);
    }

    // ---- the return page state (PAY-10) -------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-10")]
    public async Task TheBrowserComingBackBeforeTheWebhook_ShowsConfirming_NeverPaid()
    {
        using var rig = new PaymentRig(postgres);
        var placed = await rig.PlaceAndStartAsync();

        var status = await rig.Host.SendAsync(new GetPaymentStatusQuery(placed.Token));

        Assert.Equal("confirming", status!.State);
        Assert.Equal((placed.OrderNumber, Total, "CARD"), (status.OrderNumber, status.Total, status.MethodCode));
    }

    [Fact]
    [Trait("Spec", "PAY-10")]
    public async Task TheStatus_FollowsTheStoredState_ThroughPaidReviewExpiredAndCod()
    {
        using var rig = new PaymentRig(postgres);
        var paid = await rig.PlaceAndStartAsync();
        await rig.DeliverAsync(paid.GatewayRef!, GatewayOutcome.Paid);
        var review = await rig.PlaceAndStartAsync();
        await rig.DeliverAsync(review.GatewayRef!, GatewayOutcome.Paid, amount: 5m);
        var cod = await rig.Host.SendAsync(PaymentRig.Place(PaymentMethods.CashOnDelivery));

        Assert.Equal("paid", (await rig.Host.SendAsync(new GetPaymentStatusQuery(paid.Token)))!.State);
        Assert.Equal("review", (await rig.Host.SendAsync(new GetPaymentStatusQuery(review.Token)))!.State);
        Assert.Equal("cod", (await rig.Host.SendAsync(new GetPaymentStatusQuery($"pay-{cod.OrderNumber}")))!.State);

        var expiring = await rig.PlaceAndStartAsync();
        rig.Advance(TimeSpan.FromMinutes(31));
        await rig.Host.SendAsync(new ExpireUnpaidOrdersCommand());
        Assert.Equal("expired", (await rig.Host.SendAsync(new GetPaymentStatusQuery(expiring.Token)))!.State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("junk")]
    [InlineData("pay-RMP-2026-99999-9")]
    public async Task TheStatusOfAnUnknownOrBadToken_IsNotFound_WithNoDetail(string? token)
    {
        using var rig = new PaymentRig(postgres);

        Assert.Null(await rig.Host.SendAsync(new GetPaymentStatusQuery(token)));
    }

    // ---- Y7/Y9: what is offered while the gateway is unhealthy -----------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-7")]
    [Trait("Spec", "PAY-14")]
    public async Task TheCheckoutOffersOnlineMethods_UntilTheCircuitOpens_ThenCashOnDeliveryOnly_ThenRecovers()
    {
        using var rig = new PaymentRig(postgres);
        rig.Host.Options.EnabledOnlineMethods = ["EASY", "JAZZ", "CARD"];
        Assert.Equal(["COD", "EASY", "JAZZ", "CARD"], PaymentMethodCatalog.Available(rig.Host.Options, rig.Host.Resilient.IsAvailable).Select(m => m.Code));

        rig.Gateway.Mode = FakeGatewayMode.Down;
        var placed = await rig.PlaceOnlineAsync();
        for (var i = 0; i < rig.Host.Options.CircuitFailureThreshold; i++)
        {
            rig.Host.Circuit.RecordFailure();
        }

        Assert.Equal(["COD"], PaymentMethodCatalog.Available(rig.Host.Options, rig.Host.Resilient.IsAvailable).Select(m => m.Code));
        Assert.True(rig.Host.Resilient.IsPaused);

        rig.Gateway.Mode = FakeGatewayMode.Normal;
        rig.Advance(TimeSpan.FromSeconds(31));
        var trial = await rig.Host.SendAsync(new StartOnlinePaymentCommand(placed.Token));

        Assert.Equal(StartPaymentKind.Started, trial.Kind);
        Assert.Equal(["COD", "EASY", "JAZZ", "CARD"], PaymentMethodCatalog.Available(rig.Host.Options, rig.Host.Resilient.IsAvailable).Select(m => m.Code));
    }

    [Fact]
    [Trait("Spec", "PAY-1")]
    public async Task EasypaisaAndJazzCash_FollowTheSameFlowAsCards()
    {
        using var rig = new PaymentRig(postgres);
        foreach (var method in new[] { PaymentMethods.Easypaisa, PaymentMethods.JazzCash })
        {
            var placed = await rig.PlaceAndStartAsync(method);

            await rig.DeliverAsync(placed.GatewayRef!, GatewayOutcome.Paid);

            Assert.Equal(OrderStatuses.Paid, (await rig.OrderAsync(placed.OrderNumber)).StatusId);
            Assert.Equal(PaymentMethods.CodeOf(method), rig.Gateway.Sessions.Single(s => s.OrderNumber == placed.OrderNumber).MethodCode);
        }
    }
}
