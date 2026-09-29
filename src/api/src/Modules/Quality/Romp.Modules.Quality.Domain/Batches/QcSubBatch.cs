using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Domain.Outcomes;

namespace Romp.Modules.Quality.Domain.Batches;

/// <summary>
/// Maps to QC.QC_BTCH_SPLT: a part of a batch. A whole-batch decision is one part (number 1). A part is created decided
/// (first decision), or as the returned quantity of a rework part, awaiting re-inspection. The outcome is empty until decided.
/// </summary>
public sealed class QcSubBatch : Entity<long>, IAuditable
{
    private readonly List<QcSubBatchLine> _lines = [];
    private readonly List<ReworkReturn> _returns = [];

    private QcSubBatch()
    {
    }

    public long BatchId { get; private set; }

    public short SplitNo { get; private set; }

    /// <summary>The rework part this quantity came back from; null for a part of the first decision.</summary>
    public long? ParentSubBatchId { get; private set; }

    public short? OutcomeId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? ActingPerson { get; private set; }

    /// <summary>How many times this quantity has been sent back for rework (rounds are unlimited, AC-26).</summary>
    public short ReworkRoundCount { get; private set; }

    public short StatusId { get; private set; }

    public IReadOnlyCollection<QcSubBatchLine> Lines => _lines.AsReadOnly();

    public IReadOnlyCollection<ReworkReturn> Returns => _returns.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>A part of the first decision: decided at once (or sent for rework).</summary>
    internal static QcSubBatch Decided(short splitNo, DecisionPart part, string actingPerson, DateTimeOffset now, long? parentId, short reworkRounds)
    {
        var sub = new QcSubBatch { SplitNo = splitNo, ParentSubBatchId = parentId, ReworkRoundCount = reworkRounds };
        sub._lines.AddRange(part.Lines.Select(l => new QcSubBatchLine(l.SizeId, l.ColourId, l.Qty)));
        sub.Decide(part.OutcomeId, part.Reason, actingPerson, now);
        return sub;
    }

    /// <summary>The returned units of a rework part: a new part awaiting re-inspection.</summary>
    internal static QcSubBatch Returned(short splitNo, QcSubBatch parent, IEnumerable<PartLine> lines)
    {
        var sub = new QcSubBatch { SplitNo = splitNo, ParentSubBatchId = parent.Id, ReworkRoundCount = parent.ReworkRoundCount, StatusId = BatchStatus.Pending };
        sub._lines.AddRange(lines.Select(l => new QcSubBatchLine(l.SizeId, l.ColourId, l.Qty)));
        return sub;
    }

    internal void MarkInInspection() => StatusId = BatchStatus.InInspection;

    internal void Decide(short outcomeId, string? reason, string actingPerson, DateTimeOffset now)
    {
        OutcomeId = outcomeId;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ActingPerson = actingPerson;
        DecidedAt = now;
        if (outcomeId == QcOutcome.Rework)
        {
            ReworkRoundCount++;
            StatusId = BatchStatus.AwaitingRework;
        }
        else
        {
            StatusId = BatchStatus.Decided;
        }
    }

    /// <summary>Notes a return against this rework part. Once every sent unit has come back the part is settled.</summary>
    internal void AddReturn(DateOnly returnDate, string actingPerson, string? note, IReadOnlyList<PartLine> lines)
    {
        foreach (var line in lines)
        {
            _lines.Single(l => l.SizeId == line.SizeId && l.ColourId == line.ColourId).AddReturned(line.Qty);
        }

        _returns.Add(new ReworkReturn(returnDate, actingPerson, note));
        if (_lines.All(l => l.OutstandingQty == 0))
        {
            StatusId = BatchStatus.Decided;
        }
    }
}

/// <summary>Maps to QC.QC_BTCH_SPLT_LINE. <see cref="ReturnedQty"/> is the running total returned from the vendor after rework.</summary>
public sealed class QcSubBatchLine : Entity<long>, IAuditable
{
    private QcSubBatchLine()
    {
    }

    internal QcSubBatchLine(short sizeId, short colourId, int qty)
    {
        SizeId = sizeId;
        ColourId = colourId;
        Qty = qty;
    }

    public long SubBatchId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int Qty { get; private set; }

    public int ReturnedQty { get; private set; }

    /// <summary>What a rework part has not yet got back from the vendor (AC-27).</summary>
    public int OutstandingQty => Qty - ReturnedQty;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    internal void AddReturned(int qty) => ReturnedQty += qty;
}

/// <summary>Maps to QC.QC_RWRK_RTRN: one return of reworked goods from the vendor.</summary>
public sealed class ReworkReturn : Entity<long>, IAuditable
{
    private ReworkReturn()
    {
        ActingPerson = string.Empty;
    }

    internal ReworkReturn(DateOnly returnDate, string actingPerson, string? note)
    {
        ReturnDate = returnDate;
        ActingPerson = actingPerson;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public long SubBatchId { get; private set; }

    public DateOnly ReturnDate { get; private set; }

    public string ActingPerson { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
