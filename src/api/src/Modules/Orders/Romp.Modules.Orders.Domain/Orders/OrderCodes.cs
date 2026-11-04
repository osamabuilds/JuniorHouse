using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Orders.Domain.Orders;

/// <summary>A lookup row of the Orders schema (standard shape: id, stable code, name, description, sort, active).</summary>
public abstract class OrdersLookup
{
    public short Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>ORDR.ORDR_STS_LKP. Dispatched and Delivered are written by the fulfilment sprints (S15).</summary>
public static class OrderStatuses
{
    public const short PendingConfirmation = 1;
    public const short Confirmed = 2;
    public const short PaymentPending = 3;
    public const short Paid = 4;
    public const short Cancelled = 5;
    public const short Dispatched = 6;
    public const short Delivered = 7;

    public static bool IsKnown(short id) => id is >= PendingConfirmation and <= Delivered;

    public static string CodeOf(short id) => id switch
    {
        PendingConfirmation => "PNDC",
        Confirmed => "CNFD",
        PaymentPending => "PYPN",
        Paid => "PAID",
        Cancelled => "CNCL",
        Dispatched => "DSPT",
        Delivered => "DLVD",
        _ => throw new DomainException($"Unknown order status {id}."),
    };
}

public sealed class OrderStatusLookup : OrdersLookup;

/// <summary>ORDR.PYMT_METH_LKP.</summary>
public static class PaymentMethods
{
    public const short CashOnDelivery = 1;
    public const short Easypaisa = 2;
    public const short JazzCash = 3;
    public const short Card = 4;

    public static bool IsKnown(short id) => id is >= CashOnDelivery and <= Card;

    public static bool IsCashOnDelivery(short id) => id == CashOnDelivery;

    public static string CodeOf(short id) => id switch
    {
        CashOnDelivery => "COD",
        Easypaisa => "EASY",
        JazzCash => "JAZZ",
        Card => "CARD",
        _ => throw new DomainException($"Unknown payment method {id}."),
    };
}

public sealed class PaymentMethodLookup : OrdersLookup;

/// <summary>ORDR.CNCL_RSN_LKP: why an order was cancelled.</summary>
public static class CancelReasons
{
    public const short Staff = 1;
    public const short CodNoAnswer = 2;
    public const short CodRefused = 3;
    public const short Customer = 4;
    public const short PaymentFailed = 5;
    public const short PaymentExpired = 6;

    public static bool IsKnown(short id) => id is >= Staff and <= PaymentExpired;

    public static string CodeOf(short id) => id switch
    {
        Staff => "STAF",
        CodNoAnswer => "CODN",
        CodRefused => "CODR",
        Customer => "CUST",
        PaymentFailed => "PYFL",
        PaymentExpired => "PYEX",
        _ => throw new DomainException($"Unknown cancellation reason {id}."),
    };
}

public sealed class CancelReasonLookup : OrdersLookup;

/// <summary>ORDR.ACTR_KIND_LKP: who moved an order.</summary>
public static class ActorKinds
{
    public const short Staff = 1;
    public const short Customer = 2;
    public const short System = 3;
    public const short Gateway = 4;

    public static bool IsKnown(short id) => id is >= Staff and <= Gateway;
}

public sealed class ActorKindLookup : OrdersLookup;
