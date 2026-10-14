using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Warehouse.Domain;

// The stable ids and codes of every Warehouse lookup (docs/db/naming.md: an enumerated value is a lookup row, and code refers to it
// by these constants). The rows themselves are seeded by migration.

/// <summary>WHSE.GRD_LKP: the stock grade is a first-class dimension through putaway, bins and the ledger (D2).</summary>
public static class Grades
{
    public const short Accepted = 1;
    public const short Concession = 2;

    public static bool IsKnown(short id) => id is Accepted or Concession;

    public static string CodeOf(short id) => id switch
    {
        Accepted => "ACCEPTED",
        Concession => "CONCESSION",
        _ => throw new DomainException($"Unknown stock grade {id}."),
    };
}

/// <summary>WHSE.MVMT_KIND_LKP: what produced a ledger movement.</summary>
public static class MovementKinds
{
    public const short Putaway = 1;
    public const short Adjustment = 2;

    public static bool IsKnown(short id) => id is Putaway or Adjustment;

    public static string CodeOf(short id) => id switch
    {
        Putaway => "PUTAWAY",
        Adjustment => "ADJUSTMENT",
        _ => throw new DomainException($"Unknown movement kind {id}."),
    };
}

/// <summary>
/// Base shape of every Warehouse lookup table (docs/db/naming.md section 2): ID smallint, CODE, NAME, DSCR, SORT_SEQ,
/// ACT_IND. System-owned: seeded by migration.
/// </summary>
public abstract class WarehouseLookup : Entity<short>
{
    protected WarehouseLookup()
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

/// <summary>WHSE.GRD_LKP</summary>
public sealed class GradeLookup : WarehouseLookup;

/// <summary>WHSE.MVMT_KIND_LKP</summary>
public sealed class MovementKindLookup : WarehouseLookup;
