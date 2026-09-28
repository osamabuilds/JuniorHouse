using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Reference.Domain;

/// <summary>
/// Base shape for every REF lookup table (docs/db/naming.md section 2: ID smallint PK, CODE
/// unique, NAME, DSCR, SORT_SEQ, ACT_IND). A retired row (<see cref="IsActive"/> false) is kept
/// forever, never deleted, so historical records that reference it stay valid (spec AC-2). Not an
/// <see cref="AggregateRoot{TId}"/> - lookups raise no domain events and carry no audit columns
/// (they're seeded by migration, not created/edited through the normal audit-tracked flow, even
/// though staff can add/retire rows through the admin screen).
/// </summary>
public abstract class Lookup : Entity<short>
{
    protected Lookup()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    protected Lookup(string code, string name, string? description, short sortSeq)
    {
        Code = code;
        Name = name;
        Description = description;
        SortSeq = sortSeq;
        IsActive = true;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public short SortSeq { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(string name, string? description, short sortSeq)
    {
        Name = name;
        Description = description;
        SortSeq = sortSeq;
    }

    /// <summary>Retires the row (AC-2): excluded from new selection, but never deleted.</summary>
    public void Retire() => IsActive = false;
}
