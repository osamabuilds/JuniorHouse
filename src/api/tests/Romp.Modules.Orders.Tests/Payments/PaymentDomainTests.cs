using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;
using Romp.Modules.Orders.Tests.Orders;

namespace Romp.Modules.Orders.Tests.Payments;

/// <summary>The payment rules with no database: when money counts, when it does not, and that a repeat or a second charge can never count twice.</summary>
public sealed class PaymentDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);
    private const decimal Total = 3150m;

    private static Payment Online(short method = PaymentMethods.Card) => Payment.ForOnline(1, method, Total, Now.AddMinutes(30));

    private static Payment WithOpenAttempt(string gatewayRef = "ref-1")
    {
        var payment = Online();
        payment.BeginAttempt(gatewayRef, "/pay/" + gatewayRef, Now, 3);
        return payment;
    }

    [Fact]
    [Trait("Spec", "PAY-1")]
    public void ThePaymentOfACashOnDeliveryOrder_IsOwedOnDelivery_AndCannotBeStartedOnline()
    {
        var payment = Payment.ForCashOnDelivery(1, Total);

        Assert.Equal(PaymentStatuses.Pending, payment.StatusId);
        Assert.Null(payment.WindowEndsAt);
        Assert.Equal(StartDecision.NotOnline, payment.DecideStart(Now, 3));
    }

    [Theory]
    [Trait("Spec", "SEC-09")]
    [InlineData(0)]
    [InlineData(-5)]
    public void APayment_NeedsAPositiveAmount(int amount)
    {
        Assert.Throws<DomainException>(() => Payment.ForOnline(1, PaymentMethods.Card, amount, Now));
        Assert.Throws<DomainException>(() => Payment.ForCashOnDelivery(1, amount));
    }

    [Fact]
    public void AnOnlinePayment_NeedsAnOnlineMethod()
    {
        Assert.Throws<DomainException>(() => Payment.ForOnline(1, PaymentMethods.CashOnDelivery, Total, Now));
        Assert.Throws<DomainException>(() => Payment.ForOnline(1, 99, Total, Now));
    }

    [Fact]
    [Trait("Spec", "PAY-1")]
    public void TheFirstVerifiedSuccessForTheFullAmount_MarksItPaid()
    {
        var payment = WithOpenAttempt();

        var result = payment.Settle("ref-1", GatewayOutcome.Paid, Total, orderPayable: true, Now.AddMinutes(1));

        Assert.Equal(Settlement.Paid, result);
        Assert.Equal(PaymentStatuses.Paid, payment.StatusId);
        Assert.Equal(Now.AddMinutes(1), payment.PaidAt);
        Assert.Null(payment.ReviewReasonId);
    }

    [Fact]
    [Trait("Spec", "PAY-5")]
    public void TheSameSuccessTwice_ChangesNothing_TheSecondTime()
    {
        var payment = WithOpenAttempt();
        payment.Settle("ref-1", GatewayOutcome.Paid, Total, true, Now);

        var again = payment.Settle("ref-1", GatewayOutcome.Paid, Total, true, Now.AddMinutes(5));

        Assert.Equal(Settlement.AlreadyApplied, again);
        Assert.Equal(Now, payment.PaidAt);
        Assert.Null(payment.ReviewReasonId);
    }

    [Theory]
    [Trait("Spec", "PAY-8")]
    [InlineData(3149.99)]
    [InlineData(3150.01)]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnAmountThatIsNotTheOrderTotal_IsHeldForReview_NeverConfirmed(double reported)
    {
        var payment = WithOpenAttempt();

        var result = payment.Settle("ref-1", GatewayOutcome.Paid, (decimal)reported, true, Now);

        Assert.Equal(Settlement.AmountMismatch, result);
        Assert.Equal(PaymentStatuses.InReview, payment.StatusId);
        Assert.Equal(PaymentReviewReasons.AmountMismatch, payment.ReviewReasonId);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    [Trait("Spec", "PAY-11")]
    public void MoneyForAnOrderThatCanNoLongerBePaid_IsHeldForReview()
    {
        var payment = WithOpenAttempt();

        var result = payment.Settle("ref-1", GatewayOutcome.Paid, Total, orderPayable: false, Now);

        Assert.Equal(Settlement.NotPayable, result);
        Assert.Equal(PaymentStatuses.InReview, payment.StatusId);
        Assert.Equal(PaymentReviewReasons.OrderNotPayable, payment.ReviewReasonId);
    }

    [Fact]
    [Trait("Spec", "PAY-3")]
    public void ASecondAttemptThatIsAlsoCharged_IsFlaggedAsADuplicate_AndTheFirstStaysPaid()
    {
        var payment = Online();
        payment.BeginAttempt("ref-1", "/pay/1", Now, 3);
        payment.Settle("ref-1", GatewayOutcome.Failed, 0m, true, Now);
        payment.BeginAttempt("ref-2", "/pay/2", Now.AddMinutes(1), 3);
        payment.Settle("ref-2", GatewayOutcome.Paid, Total, true, Now.AddMinutes(2));

        // The first (declined) try turns out to have been charged after all, late.
        var late = payment.Settle("ref-1", GatewayOutcome.Paid, Total, true, Now.AddMinutes(3));

        Assert.Equal(Settlement.DuplicateCharge, late);
        Assert.Equal(PaymentStatuses.Paid, payment.StatusId);
        Assert.Equal(Now.AddMinutes(2), payment.PaidAt);
        Assert.Equal(PaymentReviewReasons.DuplicateCharge, payment.ReviewReasonId);
    }

    [Fact]
    public void AFailure_NeverUndoesASuccess()
    {
        var payment = WithOpenAttempt();
        payment.Settle("ref-1", GatewayOutcome.Paid, Total, true, Now);

        var result = payment.Settle("ref-1", GatewayOutcome.Failed, 0m, true, Now.AddMinutes(1));

        Assert.Equal(Settlement.AlreadyApplied, result);
        Assert.Equal(PaymentStatuses.Paid, payment.StatusId);
    }

    [Fact]
    [Trait("Spec", "PAY-2")]
    public void ADecline_LeavesThePaymentFailed_AndAllowsAnotherTry()
    {
        var payment = WithOpenAttempt();

        var result = payment.Settle("ref-1", GatewayOutcome.Failed, 0m, true, Now.AddMinutes(1));

        Assert.Equal(Settlement.FailureRecorded, result);
        Assert.Equal(PaymentStatuses.Failed, payment.StatusId);
        Assert.Equal(StartDecision.StartNew, payment.DecideStart(Now.AddMinutes(2), 3));
    }

    [Fact]
    [Trait("Spec", "PAY-3")]
    public void WhileATryIsOpen_AnotherCannotBeStarted_TheSameSessionIsReused()
    {
        var payment = WithOpenAttempt();

        Assert.Equal(StartDecision.ReuseOpen, payment.DecideStart(Now.AddMinutes(1), 3));
        Assert.Throws<DomainException>(() => payment.BeginAttempt("ref-2", "/pay/2", Now.AddMinutes(1), 3));
        Assert.Equal("/pay/ref-1", payment.OpenAttempt!.RedirectUrl);
    }

    [Fact]
    public void AfterTheLastTry_NoMoreCanBeStarted()
    {
        var payment = Online();
        for (var i = 1; i <= 3; i++)
        {
            payment.BeginAttempt($"ref-{i}", $"/pay/{i}", Now, 3);
            payment.Settle($"ref-{i}", GatewayOutcome.Failed, 0m, true, Now);
        }

        Assert.Equal(StartDecision.TooManyAttempts, payment.DecideStart(Now, 3));
    }

    [Fact]
    [Trait("Spec", "PAY-11")]
    public void AfterTheWindow_NoTryCanBeStarted_AndThePaymentExpires()
    {
        var payment = Online();
        var after = Now.AddMinutes(31);

        Assert.Equal(StartDecision.WindowClosed, payment.DecideStart(after, 3));
        Assert.True(payment.Expire(after));
        Assert.Equal(PaymentStatuses.Expired, payment.StatusId);
        Assert.Equal(StartDecision.NotPayable, payment.DecideStart(after, 3));
    }

    [Fact]
    public void APaidPayment_CannotExpire_AndCannotBeStartedAgain()
    {
        var payment = WithOpenAttempt();
        payment.Settle("ref-1", GatewayOutcome.Paid, Total, true, Now);

        Assert.False(payment.Expire(Now.AddHours(2)));
        Assert.Equal(StartDecision.AlreadyPaid, payment.DecideStart(Now.AddHours(2), 3));
    }

    [Fact]
    [Trait("Spec", "PAY-11")]
    public void MoneyThatArrivesAfterExpiry_IsHeldForReview_NotAppliedToACancelledOrder()
    {
        var payment = WithOpenAttempt();
        payment.Expire(Now.AddMinutes(31));

        var result = payment.Settle("ref-1", GatewayOutcome.Paid, Total, orderPayable: false, Now.AddMinutes(32));

        Assert.Equal(Settlement.NotPayable, result);
        Assert.Equal(PaymentStatuses.InReview, payment.StatusId);
    }

    [Fact]
    public void AMessageForAnUnknownReference_IsUnmatched()
    {
        Assert.Equal(Settlement.Unmatched, WithOpenAttempt().Settle("other", GatewayOutcome.Paid, Total, true, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnAttempt_NeedsAGatewayReference(string reference)
    {
        Assert.Throws<DomainException>(() => Online().BeginAttempt(reference, "/pay/x", Now, 3));
    }

    [Fact]
    public void AnAttempt_NeedsAPaymentPageAddress()
    {
        Assert.Throws<DomainException>(() => Online().BeginAttempt("ref-1", string.Empty, Now, 3));
        Assert.Throws<DomainException>(() => Online().BeginAttempt("ref-1", new string('x', 501), Now, 3));
    }

    [Fact]
    [Trait("Spec", "OCF-3")]
    public void MarkingAnOnlineOrderPaid_MovesItOnce_AndRaisesOnePaidEvent()
    {
        var order = Order.Place(OrderTests.Data(PaymentMethods.Card));
        order.MarkPlaced();
        var raisedBefore = order.DomainEvents.Count;

        Assert.Equal(OrderTransition.Changed, order.MarkPaid(Now));
        Assert.Equal(OrderTransition.Unchanged, order.MarkPaid(Now.AddMinutes(1)));

        Assert.Equal(OrderStatuses.Paid, order.StatusId);
        Assert.Equal(1, order.DomainEvents.Count - raisedBefore);
    }

    [Fact]
    public void ACashOnDeliveryOrder_CannotBeMarkedPaidByTheGateway()
    {
        var order = Order.Place(OrderTests.Data(PaymentMethods.CashOnDelivery));
        order.MarkPlaced();

        Assert.Equal(OrderTransition.NotAllowed, order.MarkPaid(Now));
        Assert.Equal(OrderStatuses.PendingConfirmation, order.StatusId);
    }

    [Fact]
    public void ACancelledOrder_CannotBeMarkedPaid()
    {
        var order = Order.Place(OrderTests.Data(PaymentMethods.Card));
        order.MarkPlaced();
        order.Cancel(CancelReasons.PaymentExpired, ActorKinds.System, null, null, false, Now);

        Assert.Equal(OrderTransition.NotAllowed, order.MarkPaid(Now));
        Assert.Equal(OrderStatuses.Cancelled, order.StatusId);
    }
}
