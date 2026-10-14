using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Labelling.Domain;

// The stable ids and codes of every Labelling lookup (docs/db/naming.md: an enumerated value is a lookup row, and code
// refers to it by these constants). The rows themselves are seeded by migration.

public static class GtinStatus
{
    public const short Active = 1;
    public const short Retired = 2;
}

public static class VoidReasons
{
    public const short Error = 1;
    public const short Recalled = 2;
    public const short Other = 3;

    public static string CodeOf(short id) => id switch
    {
        Error => "Error",
        Recalled => "Recalled",
        Other => "Other",
        _ => throw new DomainException($"Unknown void reason {id}."),
    };

    public static bool IsKnown(short id) => id is Error or Recalled or Other;
}

public static class LabelWorkStatus
{
    public const short Ready = 1;
    public const short NeedsAttention = 2;
}

public static class ParkReasons
{
    public const short NoStyle = 1;
    public const short StyleRetired = 2;
    public const short UnknownCode = 3;
}

/// <summary>The outcome QC gave the quantity: it decides whether the tag carries the "2ND" mark (D6).</summary>
public static class LabelOutcome
{
    public const short Accepted = 1;
    public const short Concession = 2;

    public static string CodeOf(short id) => id switch
    {
        Accepted => "Accepted",
        Concession => "Concession",
        _ => throw new DomainException($"Unknown label outcome {id}."),
    };

    public static short? FromQcCode(string? code) => code switch
    {
        "Accepted" => Accepted,
        "Concession" => Concession,
        _ => null,
    };
}

public static class PrintJobStatus
{
    public const short Queued = 1;
    public const short Printing = 2;
    public const short Printed = 3;
    public const short Failed = 4;
    public const short Cancelled = 5;
}

public static class PrintJobKind
{
    public const short Original = 1;
    public const short Reprint = 2;

    public static string CodeOf(short id) => id == Original ? "Original" : "Reprint";
}

public static class ReprintReasons
{
    public const short Lost = 1;
    public const short Damaged = 2;
    public const short PriceChange = 3;
    public const short Other = 4;

    public static bool IsKnown(short id) => id is Lost or Damaged or PriceChange or Other;
}

public static class PrintFormat
{
    public const short Zpl = 1;
    public const short Pdf = 2;
}

public static class AttemptOutcome
{
    public const short Printed = 1;
    public const short Failed = 2;
}

/// <summary>
/// Base shape of every Labelling lookup table (docs/db/naming.md section 2): ID smallint, CODE, NAME, DSCR, SORT_SEQ,
/// ACT_IND. System-owned: seeded by migration, never edited by staff.
/// </summary>
public abstract class LabellingLookup : Entity<short>
{
    protected LabellingLookup()
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

/// <summary>SKU.GTIN_STS_LKP</summary>
public sealed class GtinStatusLookup : LabellingLookup;

/// <summary>SKU.VOID_RSN_LKP</summary>
public sealed class VoidReasonLookup : LabellingLookup;

/// <summary>SKU.LBL_WORK_STS_LKP</summary>
public sealed class LabelWorkStatusLookup : LabellingLookup;

/// <summary>SKU.PARK_RSN_LKP</summary>
public sealed class ParkReasonLookup : LabellingLookup;

/// <summary>SKU.LBL_OUTC_LKP</summary>
public sealed class LabelOutcomeLookup : LabellingLookup;

/// <summary>SKU.PRT_JOB_STS_LKP</summary>
public sealed class PrintJobStatusLookup : LabellingLookup;

/// <summary>SKU.PRT_JOB_KIND_LKP</summary>
public sealed class PrintJobKindLookup : LabellingLookup;

/// <summary>SKU.RPRT_RSN_LKP</summary>
public sealed class ReprintReasonLookup : LabellingLookup;

/// <summary>SKU.PRT_FMT_LKP</summary>
public sealed class PrintFormatLookup : LabellingLookup;

/// <summary>SKU.PRT_ATMP_OTCM_LKP</summary>
public sealed class AttemptOutcomeLookup : LabellingLookup;

/// <summary>
/// SKU.PRTR_LKP: a printer the worker may send to. Configuration data, seeded by migration; no endpoint can add or change one
/// (ADR 0010, PJ-12).
/// </summary>
public sealed class PrinterLookup : LabellingLookup
{
    public short FormatId { get; private set; }

    public string Address { get; private set; } = string.Empty;
}
