using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Contracts;

namespace Romp.Modules.Shipping.Domain;

// The stable ids and codes of every Shipping lookup (docs/db/naming.md: an enumerated value is a lookup row). The rows are seeded by migration.

/// <summary>SHPG.SHPG_STS_LKP: where a shipment is. The rank says which statuses may follow which (TRK-4).</summary>
public static class ShipmentStatuses
{
    public const short PendingBooking = 1;
    public const short Booked = 2;
    public const short PickedUp = 3;
    public const short InTransit = 4;
    public const short OutForDelivery = 5;
    public const short FailedAttempt = 6;
    public const short Delivered = 7;
    public const short ReturnedToOrigin = 8;
    public const short Cancelled = 9;

    public static bool IsKnown(short id) => id is >= PendingBooking and <= Cancelled;

    /// <summary>Progress along the journey. A status with a lower rank never replaces a higher one; out for delivery and failed attempt share a rank because a re-attempt follows a failure.</summary>
    public static int RankOf(short id) => id switch
    {
        PendingBooking => 0,
        Booked => 1,
        PickedUp => 2,
        InTransit => 3,
        OutForDelivery or FailedAttempt => 4,
        Delivered or ReturnedToOrigin => 5,
        Cancelled => 6,
        _ => throw new DomainException($"Unknown shipment status {id}."),
    };

    public static bool IsFinal(short id) => id is Delivered or ReturnedToOrigin or Cancelled;

    public static string CodeOf(short id) => id switch
    {
        PendingBooking => ShipmentStatusCodes.PendingBooking,
        Booked => ShipmentStatusCodes.Booked,
        PickedUp => ShipmentStatusCodes.PickedUp,
        InTransit => ShipmentStatusCodes.InTransit,
        OutForDelivery => ShipmentStatusCodes.OutForDelivery,
        FailedAttempt => ShipmentStatusCodes.FailedAttempt,
        Delivered => ShipmentStatusCodes.Delivered,
        ReturnedToOrigin => ShipmentStatusCodes.ReturnedToOrigin,
        Cancelled => ShipmentStatusCodes.Cancelled,
        _ => throw new DomainException($"Unknown shipment status {id}."),
    };

    public static string NameOf(short id) => id switch
    {
        PendingBooking => "Preparing your parcel",
        Booked => "Booked with the courier",
        PickedUp => "Picked up by the courier",
        InTransit => "On its way",
        OutForDelivery => "Out for delivery",
        FailedAttempt => "Delivery attempt failed",
        Delivered => "Delivered",
        ReturnedToOrigin => "Returned to us",
        Cancelled => "Cancelled",
        _ => throw new DomainException($"Unknown shipment status {id}."),
    };

    public static short? FromCode(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        ShipmentStatusCodes.PendingBooking => PendingBooking,
        ShipmentStatusCodes.Booked => Booked,
        ShipmentStatusCodes.PickedUp => PickedUp,
        ShipmentStatusCodes.InTransit => InTransit,
        ShipmentStatusCodes.OutForDelivery => OutForDelivery,
        ShipmentStatusCodes.FailedAttempt => FailedAttempt,
        ShipmentStatusCodes.Delivered => Delivered,
        ShipmentStatusCodes.ReturnedToOrigin => ReturnedToOrigin,
        ShipmentStatusCodes.Cancelled => Cancelled,
        _ => null,
    };
}

/// <summary>SHPG.CRER_LKP: the couriers. A real courier is a new lookup row plus a gateway class (ADR 0031).</summary>
public static class Couriers
{
    public const short Primary = 1;
    public const short Secondary = 2;

    public static string CodeOf(short id) => id switch
    {
        Primary => "PRIMARY",
        Secondary => "SECONDARY",
        _ => throw new DomainException($"Unknown courier {id}."),
    };

    public static string NameOf(short id) => id switch
    {
        Primary => "Primary courier",
        Secondary => "Secondary courier",
        _ => throw new DomainException($"Unknown courier {id}."),
    };

    public static short? FromCode(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "PRIMARY" => Primary,
        "SECONDARY" => Secondary,
        _ => null,
    };
}

/// <summary>SHPG.BKNG_SRC_LKP: how a waybill got onto the shipment.</summary>
public static class BookingSources
{
    public const short Courier = 1;
    public const short Manual = 2;
}

/// <summary>SHPG.EVNT_SRC_LKP: where a status event came from.</summary>
public static class EventSources
{
    public const short Push = 1;
    public const short Poll = 2;
    public const short System = 3;
}

/// <summary>SHPG.MNFS_STS_LKP</summary>
public static class ManifestStatuses
{
    public const short Open = 1;
    public const short Matched = 2;
    public const short Mismatch = 3;
    public const short Closed = 4;

    public static string CodeOf(short id) => id switch
    {
        Open => "OPEN",
        Matched => "MATCHED",
        Mismatch => "MISMATCH",
        Closed => "CLOSED",
        _ => throw new DomainException($"Unknown manifest status {id}."),
    };
}

/// <summary>Why a booking did not happen. Never carries courier text beyond a short, cleaned reason (SEC-25).</summary>
public static class BookingErrors
{
    public const string Unreachable = "UNREACHABLE";
    public const string Rejected = "REJECTED";
}

/// <summary>Base shape of every Shipping lookup (docs/db/naming.md section 2): ID smallint, CODE, NAME, DSCR, SORT_SEQ, ACT_IND. Seeded by migration.</summary>
public abstract class ShippingLookup : Entity<short>
{
    protected ShippingLookup()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public short SortSeq { get; private set; }

    public bool IsActive { get; private set; } = true;
}

public sealed class ShipmentStatusLookup : ShippingLookup;

public sealed class CourierLookup : ShippingLookup;

public sealed class BookingSourceLookup : ShippingLookup;

public sealed class EventSourceLookup : ShippingLookup;

public sealed class ManifestStatusLookup : ShippingLookup;

/// <summary>SHPG.COD_STS_LKP: where the cash of a parcel stands (FR-SC-17).</summary>
public static class CodStatuses
{
    public const short NotCod = 1;
    public const short Awaiting = 2;
    public const short Settled = 3;
    public const short ShortPaid = 4;
    public const short Over = 5;
    public const short Resolved = 6;

    public static string CodeOf(short id) => id switch
    {
        NotCod => "NOT_COD",
        Awaiting => "AWAITING",
        Settled => "SETTLED",
        ShortPaid => "SHORT",
        Over => "OVER",
        Resolved => "RESOLVED",
        _ => throw new DomainException($"Unknown COD status {id}."),
    };
}

/// <summary>SHPG.RMTN_OTCM_LKP: what a statement line was judged to be.</summary>
public static class RemittanceOutcomes
{
    public const short Matched = 1;
    public const short ShortPaid = 2;
    public const short Over = 3;
    public const short Duplicate = 4;
    public const short Unknown = 5;
    public const short NotDelivered = 6;
    public const short NotCod = 7;

    public static string CodeOf(short id) => id switch
    {
        Matched => "MATCHED",
        ShortPaid => "SHORT",
        Over => "OVER",
        Duplicate => "DUPLICATE",
        Unknown => "UNKNOWN",
        NotDelivered => "NOT_DELIVERED",
        NotCod => "NOT_COD",
        _ => throw new DomainException($"Unknown remittance outcome {id}."),
    };

    public static string NameOf(short id) => id switch
    {
        Matched => "Matched",
        ShortPaid => "Short: less cash than the parcel needed",
        Over => "Over: more cash than the parcel needed",
        Duplicate => "Paid before",
        Unknown => "Unknown waybill",
        NotDelivered => "Not delivered yet",
        NotCod => "Not a cash parcel",
        _ => throw new DomainException($"Unknown remittance outcome {id}."),
    };
}

public sealed class CodStatusLookup : ShippingLookup;

public sealed class RemittanceOutcomeLookup : ShippingLookup;
