using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Domain.Payments;

/// <summary>ORDR.PYMT_STS_LKP: where a payment stands. Cash on delivery is Pending until the courier remits (S16).</summary>
public static class PaymentStatuses
{
    public const short Initiated = 1;
    public const short Pending = 2;
    public const short Paid = 3;
    public const short Failed = 4;
    public const short Expired = 5;
    public const short InReview = 6;

    public static bool IsKnown(short id) => id is >= Initiated and <= InReview;

    public static string CodeOf(short id) => id switch
    {
        Initiated => "INIT",
        Pending => "PEND",
        Paid => "PAID",
        Failed => "FAIL",
        Expired => "EXPR",
        InReview => "REVW",
        _ => throw new DomainException($"Unknown payment status {id}."),
    };
}

public sealed class PaymentStatusLookup : OrdersLookup;

/// <summary>ORDR.PYMT_RVW_LKP: why staff must look at a payment (PAY-8, duplicate charge, late payment).</summary>
public static class PaymentReviewReasons
{
    public const short AmountMismatch = 1;
    public const short OrderNotPayable = 2;
    public const short DuplicateCharge = 3;

    public static bool IsKnown(short id) => id is >= AmountMismatch and <= DuplicateCharge;

    public static string CodeOf(short id) => id switch
    {
        AmountMismatch => "AMNT",
        OrderNotPayable => "LATE",
        DuplicateCharge => "DUPL",
        _ => throw new DomainException($"Unknown payment review reason {id}."),
    };
}

public sealed class PaymentReviewReasonLookup : OrdersLookup;

/// <summary>ORDR.WBHK_OTCM_LKP: what happened to one received gateway message.</summary>
public static class WebhookOutcomes
{
    public const short Rejected = 1;
    public const short Applied = 2;
    public const short Duplicate = 3;
    public const short Unmatched = 4;
    public const short Flagged = 5;
    public const short Ignored = 6;

    public static bool IsKnown(short id) => id is >= Rejected and <= Ignored;
}

public sealed class WebhookOutcomeLookup : OrdersLookup;

/// <summary>What the gateway says happened to one attempt.</summary>
public enum GatewayOutcome
{
    Paid,
    Failed,
}

/// <summary>What recording a gateway result did to the payment.</summary>
public enum Settlement
{
    /// <summary>The first success: the payment is paid and the order may be marked paid.</summary>
    Paid,

    /// <summary>A failure was recorded; the shopper may retry if attempts are left.</summary>
    FailureRecorded,

    /// <summary>A repeat of something already recorded: nothing changed (PAY-5).</summary>
    AlreadyApplied,

    /// <summary>The amount is not the order total: held for review, the order is not confirmed (PAY-8).</summary>
    AmountMismatch,

    /// <summary>The order can no longer be paid (cancelled or expired): held for review, a refund is due (S17).</summary>
    NotPayable,

    /// <summary>A second attempt was charged after one had already paid: a refund is due (S17).</summary>
    DuplicateCharge,

    /// <summary>No attempt has this reference.</summary>
    Unmatched,
}

/// <summary>What starting (or restarting) a payment would do.</summary>
public enum StartDecision
{
    /// <summary>Make a new gateway session.</summary>
    StartNew,

    /// <summary>A session is already open and unanswered: hand back the same one, never make a second (no double charge).</summary>
    ReuseOpen,

    AlreadyPaid,
    WindowClosed,
    TooManyAttempts,
    NotOnline,

    /// <summary>The payment is under review or otherwise closed: nothing may be started.</summary>
    NotPayable,
}
