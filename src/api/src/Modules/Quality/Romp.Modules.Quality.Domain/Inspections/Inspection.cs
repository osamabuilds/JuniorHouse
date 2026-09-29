using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Domain.SafetyChecks;

namespace Romp.Modules.Quality.Domain.Inspections;

/// <summary>Maps to QC.QC_INSP_MAIN: one inspection round on a batch or on a sub-batch returned from rework.</summary>
public sealed class Inspection : Entity<long>, IAuditable
{
    private readonly List<InspectionCheck> _checks = [];
    private readonly List<Defect> _defects = [];
    private readonly List<SafetyCheck> _safetyChecks = [];

    private Inspection()
    {
        ActingPerson = string.Empty;
    }

    public long BatchId { get; private set; }

    /// <summary>Null when the whole batch is inspected (first pass); the sub-batch id for a rework re-inspection.</summary>
    public long? SubBatchId { get; private set; }

    public short RoundNo { get; private set; }

    public short ModeId { get; private set; }

    public short InspectionLevelId { get; private set; }

    public short AqlLevelId { get; private set; }

    public int LotQty { get; private set; }

    public int SampleQty { get; private set; }

    public int AcceptNo { get; private set; }

    public int RejectNo { get; private set; }

    public short ResultId { get; private set; }

    public DateOnly InspectionDate { get; private set; }

    public string ActingPerson { get; private set; }

    public IReadOnlyCollection<InspectionCheck> Checks => _checks.AsReadOnly();

    public IReadOnlyCollection<Defect> Defects => _defects.AsReadOnly();

    public IReadOnlyCollection<SafetyCheck> SafetyChecks => _safetyChecks.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to QC.QC_INSP_CHK: the result of one tech-pack checklist item.</summary>
public sealed class InspectionCheck : Entity<long>, IAuditable
{
    private InspectionCheck()
    {
    }

    public long InspectionId { get; private set; }

    public short CheckTypeId { get; private set; }

    public short ResultId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to QC.QC_DFCT: a recorded defect. A written note is mandatory (AC-17).</summary>
public sealed class Defect : Entity<long>, IAuditable
{
    private Defect()
    {
        Note = string.Empty;
    }

    public long InspectionId { get; private set; }

    public short DefectTypeId { get; private set; }

    public short SeverityId { get; private set; }

    public int Qty { get; private set; }

    public short? SizeId { get; private set; }

    public short? ColourId { get; private set; }

    public string Note { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
