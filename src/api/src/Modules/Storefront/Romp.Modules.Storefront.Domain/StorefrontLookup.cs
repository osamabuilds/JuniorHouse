using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Storefront.Domain;

/// <summary>
/// Standard lookup shape (docs/db/naming.md): <c>ID</c>, <c>CODE</c>, <c>NAME</c>, <c>DSCR</c>, <c>SORT_SEQ</c>, <c>ACT_IND</c>. Rows are seeded by
/// migration. The key type is the enum (or smallint) the entities use, so a foreign key and its lookup agree on the CLR type. Shared by both
/// Storefront features, so it lives at the module root rather than in either.
/// </summary>
public abstract class StorefrontLookup<TId> : Entity<TId>
    where TId : notnull
{
    protected StorefrontLookup()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public short SortSeq { get; private set; }

    public bool IsActive { get; private set; }
}
