using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Quality.Domain;

/// <summary>
/// Base shape of every QC lookup table (docs/db/naming.md section 2): ID smallint, CODE, NAME,
/// DSCR, SORT_SEQ, ACT_IND. QC's lookups are system-owned: seeded by migration, never edited by staff.
/// </summary>
public abstract class QualityLookup : Entity<short>
{
    protected QualityLookup()
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

/// <summary>QC.GRN_STS_LKP</summary>
public sealed class GrnStatusLookup : QualityLookup;

/// <summary>QC.GRN_RCNL_LKP</summary>
public sealed class GrnReconciliationLookup : QualityLookup;

/// <summary>QC.QC_BTCH_STS_LKP</summary>
public sealed class BatchStatusLookup : QualityLookup;

/// <summary>QC.QC_OTCM_LKP</summary>
public sealed class OutcomeLookup : QualityLookup;

/// <summary>QC.QC_INSP_MODE_LKP</summary>
public sealed class InspectionModeLookup : QualityLookup;

/// <summary>QC.QC_INSP_RSLT_LKP</summary>
public sealed class InspectionResultLookup : QualityLookup;

/// <summary>QC.QC_INSP_LVL_LKP</summary>
public sealed class InspectionLevelLookup : QualityLookup;

/// <summary>QC.QC_AQL_LVL_LKP; <see cref="AqlValue"/> is the acceptable quality level in percent defects.</summary>
public sealed class AqlLevelLookup : QualityLookup
{
    public decimal AqlValue { get; private set; }
}

/// <summary>QC.QC_CHK_TYP_LKP</summary>
public sealed class CheckTypeLookup : QualityLookup;

/// <summary>QC.QC_CHK_RSLT_LKP</summary>
public sealed class CheckResultLookup : QualityLookup;

/// <summary>QC.QC_SAFE_TYP_LKP</summary>
public sealed class SafetyTypeLookup : QualityLookup;

/// <summary>QC.QC_DFCT_TYP_LKP</summary>
public sealed class DefectTypeLookup : QualityLookup;

/// <summary>QC.QC_DFCT_SVRT_LKP</summary>
public sealed class DefectSeverityLookup : QualityLookup;
