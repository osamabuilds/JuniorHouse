using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Fulfilment.Domain;

// The stable ids and codes of every Fulfilment lookup (docs/db/naming.md: an enumerated value is a lookup row). The rows are seeded by migration.

/// <summary>FULF.FULF_STS_LKP</summary>
public static class FulfilmentStatuses
{
    public const short Queued = 1;
    public const short Picking = 2;
    public const short Picked = 3;
    public const short Packing = 4;
    public const short Packed = 5;
    public const short Held = 6;
    public const short Cancelled = 7;

    public static bool IsKnown(short id) => id is >= Queued and <= Cancelled;

    public static string CodeOf(short id) => id switch
    {
        Queued => "QUEUED",
        Picking => "PICKING",
        Picked => "PICKED",
        Packing => "PACKING",
        Packed => "PACKED",
        Held => "HELD",
        Cancelled => "CANCELLED",
        _ => throw new DomainException($"Unknown fulfilment status {id}."),
    };
}

/// <summary>FULF.LINE_STS_LKP</summary>
public static class LineStatuses
{
    public const short Allocated = 1;
    public const short AwaitingRestock = 2;
    public const short Picked = 3;
    public const short Packed = 4;
    public const short Cancelled = 5;

    public static string CodeOf(short id) => id switch
    {
        Allocated => "ALLOCATED",
        AwaitingRestock => "AWAITING_RESTOCK",
        Picked => "PICKED",
        Packed => "PACKED",
        Cancelled => "CANCELLED",
        _ => throw new DomainException($"Unknown line status {id}."),
    };
}

/// <summary>FULF.EXCP_RSN_LKP: why a picker could not take the units (PEX-5).</summary>
public static class ExceptionReasons
{
    public const short NotFound = 1;
    public const short Shortfall = 2;
    public const short Damaged = 3;

    public static bool IsKnown(short id) => id is NotFound or Shortfall or Damaged;

    public static string CodeOf(short id) => id switch
    {
        NotFound => "NOT_FOUND",
        Shortfall => "SHORT",
        Damaged => "DAMAGED",
        _ => throw new DomainException($"Unknown exception reason {id}."),
    };

    public static short? FromCode(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "NOT_FOUND" => NotFound,
        "SHORT" => Shortfall,
        "DAMAGED" => Damaged,
        _ => null,
    };
}

/// <summary>FULF.EXCP_STS_LKP</summary>
public static class ExceptionStatuses
{
    public const short Open = 1;
    public const short Resolved = 2;
}

/// <summary>FULF.PACK_KIND_LKP: the packaging the rule recommends (PAK-4).</summary>
public static class PackagingKinds
{
    public const short PolyBag = 1;
    public const short Box = 2;

    public static string CodeOf(short id) => id switch
    {
        PolyBag => "POLY_BAG",
        Box => "BOX",
        _ => throw new DomainException($"Unknown packaging kind {id}."),
    };
}

/// <summary>FULF.SCAN_STG_LKP</summary>
public static class ScanStages
{
    public const short Pick = 1;
    public const short Pack = 2;
}

/// <summary>FULF.SCAN_OTCM_LKP: what a scan did.</summary>
public static class ScanOutcomes
{
    public const short Accepted = 1;
    public const short Refused = 2;

    public static string CodeOf(short id) => id == Accepted ? "ACCEPTED" : "REFUSED";
}

/// <summary>
/// Base shape of every Fulfilment lookup (docs/db/naming.md section 2): ID smallint, CODE, NAME, DSCR, SORT_SEQ, ACT_IND. System-owned:
/// seeded by migration.
/// </summary>
public abstract class FulfilmentLookup : Entity<short>
{
    protected FulfilmentLookup()
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

public sealed class FulfilmentStatusLookup : FulfilmentLookup;

public sealed class LineStatusLookup : FulfilmentLookup;

public sealed class ExceptionReasonLookup : FulfilmentLookup;

public sealed class ExceptionStatusLookup : FulfilmentLookup;

public sealed class PackagingKindLookup : FulfilmentLookup;

public sealed class ScanStageLookup : FulfilmentLookup;

public sealed class ScanOutcomeLookup : FulfilmentLookup;
