using Romp.BuildingBlocks.Domain;
using Romp.Modules.Returns.Contracts;

namespace Romp.Modules.Returns.Domain;

// The stable ids and codes of every Returns lookup (docs/db/naming.md: an enumerated value is a lookup row). The rows are seeded by migration.

/// <summary>RTRN.RTRN_KIND_LKP: why the parcel came back. Today only a parcel returned to origin (ADR 0033, decision 6).</summary>
public static class ReturnKinds
{
    public const short Rto = 1;

    public static bool IsKnown(short id) => id is Rto;
}

/// <summary>RTRN.RTRN_STS_LKP: where the case is in the gate.</summary>
public static class ReturnStatuses
{
    public const short AwaitingReceipt = 1;
    public const short InInspection = 2;
    public const short Completed = 3;

    public static bool IsKnown(short id) => id is >= AwaitingReceipt and <= Completed;

    public static string CodeOf(short id) => id switch
    {
        AwaitingReceipt => "AWAITING_RECEIPT",
        InInspection => "IN_INSPECTION",
        Completed => "COMPLETED",
        _ => throw new DomainException($"Unknown return status {id}."),
    };

    public static short? FromCode(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "AWAITING_RECEIPT" => AwaitingReceipt,
        "IN_INSPECTION" => InInspection,
        "COMPLETED" => Completed,
        _ => null,
    };
}

/// <summary>RTRN.RTRN_DCSN_LKP: what reverse QC decided for a line (section 5.20).</summary>
public static class ReturnDecisions
{
    public const short Restock = 1;
    public const short Seconds = 2;
    public const short Reject = 3;

    public static bool IsKnown(short id) => id is >= Restock and <= Reject;

    public static string CodeOf(short id) => id switch
    {
        Restock => ReturnDecisionCodes.Restock,
        Seconds => ReturnDecisionCodes.Seconds,
        Reject => ReturnDecisionCodes.Reject,
        _ => throw new DomainException($"Unknown return decision {id}."),
    };

    public static short? FromCode(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        ReturnDecisionCodes.Restock => Restock,
        ReturnDecisionCodes.Seconds => Seconds,
        ReturnDecisionCodes.Reject => Reject,
        _ => null,
    };
}

/// <summary>Base shape of every Returns lookup (docs/db/naming.md section 2): ID smallint, CODE, NAME, DSCR, SORT_SEQ, ACT_IND. Seeded by migration.</summary>
public abstract class ReturnsLookup : Entity<short>
{
    protected ReturnsLookup()
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

public sealed class ReturnKindLookup : ReturnsLookup;

public sealed class ReturnStatusLookup : ReturnsLookup;

public sealed class ReturnDecisionLookup : ReturnsLookup;
