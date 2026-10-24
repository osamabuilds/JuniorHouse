using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Storefront.Domain.StorefrontSettings;

/// <summary>
/// Standard lookup shape (docs/db/naming.md): <c>ID</c>, <c>CODE</c>, <c>NAME</c>, <c>DSCR</c>, <c>SORT_SEQ</c>, <c>ACT_IND</c>. Rows are seeded by
/// migration. The key type is the enum (or smallint) the entities use, so a foreign key and its lookup agree on the CLR type.
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

/// <summary>STFR.NAV_KIND_LKP</summary>
public sealed class NavKindLookup : StorefrontLookup<NavKind>;

/// <summary>STFR.LINK_TGT_LKP</summary>
public sealed class LinkTargetLookup : StorefrontLookup<LinkTargetKind>;

/// <summary>STFR.STNG_KEY_LKP</summary>
public sealed class SettingKeyLookup : StorefrontLookup<SettingKey>;

/// <summary>STFR.PAGE_STS_LKP</summary>
public sealed class PageStatusLookup : StorefrontLookup<PageStatus>;

/// <summary>STFR.BLCK_TYP_LKP</summary>
public sealed class BlockTypeLookup : StorefrontLookup<BlockType>;

/// <summary>STFR.FACT_LKP: the facts a page may quote (<see cref="SettingsFacts.Keys"/>), keyed by <see cref="SettingsFacts.IdOf"/>.</summary>
public sealed class FactLookup : StorefrontLookup<short>;
