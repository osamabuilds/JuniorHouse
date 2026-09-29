using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Production.Domain;

/// <summary>
/// Base shape of every PROD lookup table (docs/db/naming.md section 2): ID smallint, CODE, NAME,
/// DSCR, SORT_SEQ, ACT_IND. PROD's lookups are system-owned: seeded by migration, never edited by staff.
/// </summary>
public abstract class ProductionLookup : Entity<short>
{
    protected ProductionLookup()
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

/// <summary>PROD.PROD_RUN_STS_LKP</summary>
public sealed class RunStatusLookup : ProductionLookup;

/// <summary>PROD.MLST_TYP_LKP</summary>
public sealed class MilestoneTypeLookup : ProductionLookup;

/// <summary>PROD.PP_SMPL_DCSN_LKP</summary>
public sealed class PpDecisionLookup : ProductionLookup;

/// <summary>PROD.DLVR_TMNG_LKP</summary>
public sealed class DeliveryTimingLookup : ProductionLookup;

/// <summary>PROD.DLVR_QTY_STS_LKP</summary>
public sealed class DeliveryQuantityStatusLookup : ProductionLookup;
