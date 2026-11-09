using Romp.BuildingBlocks.Auditing;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Domain.Payments;

/// <summary>
/// One try at paying through the gateway (ORDR.PYMT_ATMP). Each has its own gateway reference, so a late message about an old try can still be
/// matched and a charge that happened twice can be seen (no double charge).
/// </summary>
public sealed class PaymentAttempt : Entity<long>, IAuditable
{
    public const int MaxReferenceLength = 64;

    public const int MaxUrlLength = 500;

    private PaymentAttempt()
    {
        GatewayRef = string.Empty;
        InsrBy = string.Empty;
    }

    internal PaymentAttempt(int attemptNo, string gatewayRef, string redirectUrl, DateTimeOffset startedAt)
    {
        AttemptNo = attemptNo;
        GatewayRef = gatewayRef;
        RedirectUrl = redirectUrl;
        StatusId = PaymentStatuses.Pending;
        StartedAt = startedAt;
        InsrBy = string.Empty;
    }

    public long PaymentId { get; private set; }

    public int AttemptNo { get; private set; }

    public string GatewayRef { get; private set; }

    /// <summary>The hosted payment page of this attempt, kept so an open attempt can be handed back instead of making a second one.</summary>
    public string? RedirectUrl { get; private set; }

    /// <summary>Pending, Paid, Failed or Expired.</summary>
    public short StatusId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? SettledAt { get; private set; }

    internal void Mark(short status, DateTimeOffset at)
    {
        StatusId = status;
        SettledAt = at;
    }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// The payment of one order (ORDR.PYMT_MAIN). It is the single place that decides whether money counts: a success is applied once (the first),
/// a repeat changes nothing, a second charge is flagged, an amount that is not the order total is held for review, and a payment for an order
/// that can no longer be paid is held for review. Amounts are compared exactly as decimals (SEC-09). No card data is ever held (PAY-9).
/// </summary>
public sealed class Payment : Entity<long>, IAuditable
{
    private readonly List<PaymentAttempt> _attempts = [];

    private Payment()
    {
        InsrBy = string.Empty;
    }

    public long OrderId { get; private set; }

    public short MethodId { get; private set; }

    public short StatusId { get; private set; }

    public decimal Amount { get; private set; }

    /// <summary>When an unpaid online order stops being payable (30 minutes by default, PAY-11); null for cash on delivery.</summary>
    public DateTimeOffset? WindowEndsAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public short? ReviewReasonId { get; private set; }

    public IReadOnlyList<PaymentAttempt> Attempts => _attempts;

    public int AttemptCount => _attempts.Count;

    public PaymentAttempt? OpenAttempt => _attempts.FirstOrDefault(a => a.StatusId == PaymentStatuses.Pending);

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>The payment row of a cash on delivery order: owed on delivery, nothing to do online (settled against the courier in S16).</summary>
    public static Payment ForCashOnDelivery(long orderId, decimal amount)
    {
        Guard(orderId, amount);
        return new Payment { OrderId = orderId, MethodId = PaymentMethods.CashOnDelivery, StatusId = PaymentStatuses.Pending, Amount = amount };
    }

    /// <summary>The payment row of an online order, before any gateway session exists.</summary>
    public static Payment ForOnline(long orderId, short methodId, decimal amount, DateTimeOffset windowEndsAt)
    {
        Guard(orderId, amount);
        if (!PaymentMethods.IsKnown(methodId) || PaymentMethods.IsCashOnDelivery(methodId))
        {
            throw new DomainException("An online payment needs an online method.");
        }

        return new Payment { OrderId = orderId, MethodId = methodId, StatusId = PaymentStatuses.Initiated, Amount = amount, WindowEndsAt = windowEndsAt };
    }

    private static void Guard(long orderId, decimal amount)
    {
        if (orderId <= 0 || amount <= 0)
        {
            throw new DomainException("A payment needs an order and a positive amount.");
        }
    }

    /// <summary>What <see cref="BeginAttempt"/> would do now. Pure: the caller asks first, so it never calls the gateway for nothing.</summary>
    public StartDecision DecideStart(DateTimeOffset now, int maxAttempts)
    {
        if (PaymentMethods.IsCashOnDelivery(MethodId))
        {
            return StartDecision.NotOnline;
        }

        return StatusId switch
        {
            PaymentStatuses.Paid => StartDecision.AlreadyPaid,
            PaymentStatuses.Expired or PaymentStatuses.InReview => StartDecision.NotPayable,
            _ when OpenAttempt is not null => StartDecision.ReuseOpen,
            _ when WindowEndsAt is { } end && now >= end => StartDecision.WindowClosed,
            _ when _attempts.Count >= maxAttempts => StartDecision.TooManyAttempts,
            _ => StartDecision.StartNew,
        };
    }

    /// <summary>Records a new gateway session. Refused unless <see cref="DecideStart"/> says <see cref="StartDecision.StartNew"/>.</summary>
    public PaymentAttempt BeginAttempt(string gatewayRef, string redirectUrl, DateTimeOffset now, int maxAttempts)
    {
        if (string.IsNullOrWhiteSpace(gatewayRef) || gatewayRef.Length > PaymentAttempt.MaxReferenceLength)
        {
            throw new DomainException("The gateway reference is not valid.");
        }

        if (string.IsNullOrWhiteSpace(redirectUrl) || redirectUrl.Length > PaymentAttempt.MaxUrlLength)
        {
            throw new DomainException("The payment page address is not valid.");
        }

        if (DecideStart(now, maxAttempts) != StartDecision.StartNew)
        {
            throw new DomainException("A new payment attempt cannot be started now.");
        }

        var attempt = new PaymentAttempt(_attempts.Count + 1, gatewayRef, redirectUrl, now);
        _attempts.Add(attempt);
        StatusId = PaymentStatuses.Pending;
        return attempt;
    }

    public PaymentAttempt? FindAttempt(string gatewayRef) => _attempts.FirstOrDefault(a => a.GatewayRef == gatewayRef);

    /// <summary>
    /// Applies what the gateway said about one attempt. <paramref name="orderPayable"/> is whether the order is still waiting for payment.
    /// Only the first success counts; a repeat of the same message is <see cref="Settlement.AlreadyApplied"/>.
    /// </summary>
    public Settlement Settle(string gatewayRef, GatewayOutcome outcome, decimal reportedAmount, bool orderPayable, DateTimeOffset now)
    {
        var attempt = FindAttempt(gatewayRef);
        if (attempt is null)
        {
            return Settlement.Unmatched;
        }

        return outcome == GatewayOutcome.Paid ? SettlePaid(attempt, reportedAmount, orderPayable, now) : SettleFailed(attempt, now);
    }

    private Settlement SettlePaid(PaymentAttempt attempt, decimal reportedAmount, bool orderPayable, DateTimeOffset now)
    {
        if (attempt.StatusId == PaymentStatuses.Paid)
        {
            return Settlement.AlreadyApplied;
        }

        // Money moved on this attempt, so it is recorded as paid whatever it means for the order.
        attempt.Mark(PaymentStatuses.Paid, now);

        if (StatusId == PaymentStatuses.Paid)
        {
            ReviewReasonId = PaymentReviewReasons.DuplicateCharge;
            return Settlement.DuplicateCharge;
        }

        if (reportedAmount != Amount)
        {
            StatusId = PaymentStatuses.InReview;
            ReviewReasonId = PaymentReviewReasons.AmountMismatch;
            return Settlement.AmountMismatch;
        }

        if (!orderPayable || StatusId is PaymentStatuses.Expired or PaymentStatuses.InReview)
        {
            StatusId = PaymentStatuses.InReview;
            ReviewReasonId = PaymentReviewReasons.OrderNotPayable;
            return Settlement.NotPayable;
        }

        StatusId = PaymentStatuses.Paid;
        PaidAt = now;
        return Settlement.Paid;
    }

    private Settlement SettleFailed(PaymentAttempt attempt, DateTimeOffset now)
    {
        // A failure never undoes a success, and a repeated failure changes nothing.
        if (attempt.StatusId != PaymentStatuses.Pending)
        {
            return Settlement.AlreadyApplied;
        }

        attempt.Mark(PaymentStatuses.Failed, now);
        if (StatusId == PaymentStatuses.Pending && OpenAttempt is null)
        {
            StatusId = PaymentStatuses.Failed;
        }

        return Settlement.FailureRecorded;
    }

    /// <summary>The window passed with nothing paid: the payment (and any open attempt) expires. False when it was already settled or closed.</summary>
    public bool Expire(DateTimeOffset now)
    {
        if (StatusId is not (PaymentStatuses.Initiated or PaymentStatuses.Pending or PaymentStatuses.Failed) || PaymentMethods.IsCashOnDelivery(MethodId))
        {
            return false;
        }

        foreach (var open in _attempts.Where(a => a.StatusId == PaymentStatuses.Pending))
        {
            open.Mark(PaymentStatuses.Expired, now);
        }

        StatusId = PaymentStatuses.Expired;
        return true;
    }
}

/// <summary>
/// One message received on the payment webhook (ORDR.PYMT_WBHK). Only a hash of the body and the fields needed are kept, never the raw payload
/// or any card or wallet detail (PAY-9). A message with a bad signature is kept too, marked as such, so attacks are visible (PAY-4).
/// </summary>
[NotAudited("Technical log written by the payment gateway callback, at the gateway's pace or an attacker's: it is itself the record, and auditing every row would flood the audit trail and the outbox.")]
public sealed class PaymentWebhook : Entity<long>, IAuditable
{
    public const int HashLength = 64;

    private PaymentWebhook()
    {
        Provider = string.Empty;
        BodyHash = string.Empty;
        InsrBy = string.Empty;
    }

    public PaymentWebhook(string provider, string? eventId, bool signatureVerified, string bodyHash, short outcomeId, string? gatewayRef, long? paymentId, DateTimeOffset receivedAt)
    {
        if (bodyHash.Length != HashLength || !WebhookOutcomes.IsKnown(outcomeId))
        {
            throw new DomainException("A webhook record needs a body hash and an outcome.");
        }

        Provider = provider;
        EventId = eventId;
        SignatureVerified = signatureVerified;
        BodyHash = bodyHash;
        OutcomeId = outcomeId;
        GatewayRef = gatewayRef;
        PaymentId = paymentId;
        ReceivedAt = receivedAt;
        InsrBy = string.Empty;
    }

    public string Provider { get; private set; }

    /// <summary>The provider's own event id; null when the message was rejected before it could be read.</summary>
    public string? EventId { get; private set; }

    public bool SignatureVerified { get; private set; }

    public string BodyHash { get; private set; }

    public short OutcomeId { get; private set; }

    public string? GatewayRef { get; private set; }

    public long? PaymentId { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
