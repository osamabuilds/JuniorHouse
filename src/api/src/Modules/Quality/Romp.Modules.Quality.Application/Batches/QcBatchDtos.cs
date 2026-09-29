using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.Inspections;

namespace Romp.Modules.Quality.Application.Batches;

public sealed record BatchLineDto(short SizeId, short ColourId, int Qty);

public sealed record CheckResultDto(short CheckTypeId, short ResultId);

public sealed record SafetyLineDto(short SizeId, short ColourId);

public sealed record SafetyCheckDto(short SafetyTypeId, short ResultId, string? Reason, IReadOnlyList<SafetyLineDto> AffectedLines);

public sealed record DefectDto(short DefectTypeId, short SeverityId, int Qty, short? SizeId, short? ColourId, string Note);

public sealed record InspectionDto(
    long Id,
    long BatchId,
    long? SubBatchId,
    short RoundNo,
    short ModeId,
    string ModeCode,
    short InspectionLevelId,
    short AqlLevelId,
    int LotQty,
    int SampleQty,
    int AcceptNo,
    int RejectNo,
    short ResultId,
    string ResultCode,
    DateOnly InspectionDate,
    string ActingPerson,
    IReadOnlyList<CheckResultDto> Checks,
    IReadOnlyList<DefectDto> Defects,
    IReadOnlyList<SafetyCheckDto> SafetyChecks);

public sealed record SubBatchLineDto(short SizeId, short ColourId, int Qty, int ReturnedQty, int OutstandingQty);

public sealed record ReworkReturnDto(DateOnly ReturnDate, string ActingPerson, string? Note);

/// <summary>
/// A part of a batch: decided at the first decision, or the returned quantity of a rework part awaiting re-inspection
/// (<c>ParentSubBatchId</c>). <c>OutstandingQty</c> per line is what a rework part has not yet got back (AC-27).
/// </summary>
public sealed record SubBatchDto(
    long Id,
    short SplitNo,
    long? ParentSubBatchId,
    short? OutcomeId,
    string? OutcomeCode,
    string? Reason,
    short StatusId,
    short ReworkRoundCount,
    DateTimeOffset? DecidedAt,
    string? ActingPerson,
    IReadOnlyList<SubBatchLineDto> Lines,
    IReadOnlyList<ReworkReturnDto> Returns);

/// <summary>The full batch. Built from QC's own data only: no PO notes, costs or target costs (AC-45).</summary>
public sealed record QcBatchDto(
    long Id,
    long GrnId,
    string GrnNo,
    string VendorDnNo,
    long PoId,
    string PoNo,
    short StatusId,
    int LotQty,
    IReadOnlyList<BatchLineDto> Lines,
    IReadOnlyList<InspectionDto> Inspections,
    IReadOnlyList<SubBatchDto> SubBatches);

public sealed record QcBatchSummaryDto(
    long Id,
    long GrnId,
    string GrnNo,
    long PoId,
    string PoNo,
    string VendorDnNo,
    short StatusId,
    int LotQty,
    short? LatestResultId);

public sealed record AqlPlanDto(short ModeId, string ModeCode, string CodeLetter, int LotQty, int SampleQty, int AcceptNo, int RejectNo);

internal static class QcBatchMapper
{
    public static InspectionDto ToDto(this Inspection i) => new(
        i.Id, i.BatchId, i.SubBatchId, i.RoundNo, i.ModeId, InspectionMode.Code(i.ModeId), i.InspectionLevelId, i.AqlLevelId,
        i.LotQty, i.SampleQty, i.AcceptNo, i.RejectNo, i.ResultId, InspectionResult.Code(i.ResultId), i.InspectionDate, i.ActingPerson,
        i.Checks.OrderBy(c => c.CheckTypeId).Select(c => new CheckResultDto(c.CheckTypeId, c.ResultId)).ToList(),
        i.Defects.OrderBy(d => d.Id).Select(d => new DefectDto(d.DefectTypeId, d.SeverityId, d.Qty, d.SizeId, d.ColourId, d.Note)).ToList(),
        i.SafetyChecks.OrderBy(s => s.SafetyTypeId).Select(s => new SafetyCheckDto(
            s.SafetyTypeId, s.ResultId, s.Reason,
            s.AffectedLines.OrderBy(l => l.SizeId).ThenBy(l => l.ColourId).Select(l => new SafetyLineDto(l.SizeId, l.ColourId)).ToList())).ToList());

    public static SubBatchDto ToDto(this QcSubBatch s) => new(
        s.Id, s.SplitNo, s.ParentSubBatchId, s.OutcomeId, s.OutcomeId is { } outcome ? QcOutcome.Code(outcome) : null, s.Reason, s.StatusId,
        s.ReworkRoundCount, s.DecidedAt, s.ActingPerson,
        s.Lines.OrderBy(l => l.SizeId).ThenBy(l => l.ColourId).Select(l => new SubBatchLineDto(l.SizeId, l.ColourId, l.Qty, l.ReturnedQty, l.OutstandingQty)).ToList(),
        s.Returns.OrderBy(r => r.ReturnDate).ThenBy(r => r.Id).Select(r => new ReworkReturnDto(r.ReturnDate, r.ActingPerson, r.Note)).ToList());

    public static AqlPlanDto ToDto(this Domain.Inspections.Aql.AqlPlan plan, int lotQty) =>
        new(plan.ModeId, InspectionMode.Code(plan.ModeId), plan.CodeLetter, lotQty, plan.SampleQty, plan.AcceptNo, plan.RejectNo);
}
