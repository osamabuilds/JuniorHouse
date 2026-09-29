using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Domain.SafetyChecks;

namespace Romp.Modules.Quality.Domain.Inspections;

/// <summary>One tech-pack checklist result as entered (spec AC-16).</summary>
public sealed record CheckInput(short CheckTypeId, short ResultId);

/// <summary>One defect as entered (spec AC-17). The note is mandatory.</summary>
public sealed record DefectInput(short DefectTypeId, short SeverityId, int Qty, short? SizeId, short? ColourId, string Note);

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

    internal Inspection(
        long? subBatchId, short roundNo, short modeId, short inspectionLevelId, short aqlLevelId, int lotQty, int sampleQty,
        int acceptNo, int rejectNo, short resultId, DateOnly inspectionDate, string actingPerson,
        IEnumerable<CheckInput> checks, IEnumerable<DefectInput> defects)
    {
        SubBatchId = subBatchId;
        RoundNo = roundNo;
        ModeId = modeId;
        InspectionLevelId = inspectionLevelId;
        AqlLevelId = aqlLevelId;
        LotQty = lotQty;
        SampleQty = sampleQty;
        AcceptNo = acceptNo;
        RejectNo = rejectNo;
        ResultId = resultId;
        InspectionDate = inspectionDate;
        ActingPerson = actingPerson;
        _checks.AddRange(checks.Select(c => new InspectionCheck(c.CheckTypeId, c.ResultId)));
        _defects.AddRange(defects.Select(d => new Defect(d)));
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

    internal void SetSafetyCheck(SafetyInput input, string actingPerson)
    {
        var existing = _safetyChecks.FirstOrDefault(s => s.SafetyTypeId == input.SafetyTypeId);
        if (existing is null)
        {
            _safetyChecks.Add(new SafetyCheck(input, actingPerson));
        }
        else
        {
            existing.Apply(input, actingPerson);
        }
    }

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

    internal InspectionCheck(short checkTypeId, short resultId)
    {
        CheckTypeId = checkTypeId;
        ResultId = resultId;
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

    internal Defect(DefectInput input)
    {
        DefectTypeId = input.DefectTypeId;
        SeverityId = input.SeverityId;
        Qty = input.Qty;
        SizeId = input.SizeId;
        ColourId = input.ColourId;
        Note = input.Note.Trim();
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
