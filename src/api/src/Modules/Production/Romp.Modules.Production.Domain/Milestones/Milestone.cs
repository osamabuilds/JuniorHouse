using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Production.Domain.Milestones;

/// <summary>Maps to PROD.PROD_MLST: one row on a run's timeline (spec AC-5).</summary>
public sealed class Milestone : Entity<long>, IAuditable
{
    private readonly List<FinishedQuantityLine> _finishedLines = [];

    private Milestone()
    {
        ReportedBy = string.Empty;
        ActingPerson = string.Empty;
    }

    internal Milestone(
        long runId, short typeId, DateOnly claimedDate, string reportedBy, string actingPerson, string? note, bool recordedLate,
        long? ppRoundId)
    {
        RunId = runId;
        TypeId = typeId;
        ClaimedDate = claimedDate;
        ReportedBy = reportedBy;
        ActingPerson = actingPerson;
        Note = note;
        IsRecordedLate = recordedLate;
        PpRoundId = ppRoundId;
    }

    public long RunId { get; private set; }

    public short TypeId { get; private set; }

    /// <summary>The date the vendor says it happened (D11 bounds apply).</summary>
    public DateOnly ClaimedDate { get; private set; }

    /// <summary>The vendor contact who reported it (free text).</summary>
    public string ReportedBy { get; private set; }

    /// <summary>The Romp person acting (D13); the system actor is in <see cref="InsrBy"/>.</summary>
    public string ActingPerson { get; private set; }

    public string? Note { get; private set; }

    /// <summary>D11/D15: recorded more than the configured days after its claimed date. Informational.</summary>
    public bool IsRecordedLate { get; private set; }

    public long? PpRoundId { get; private set; }

    /// <summary>A loud warning attached to this milestone (AC-36 / AC-38 on bulk cutting); null when there is none.</summary>
    public string? FlagReason { get; private set; }

    public bool IsFlagged => FlagReason is not null;

    public IReadOnlyCollection<FinishedQuantityLine> FinishedLines => _finishedLines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    internal void SetFlagReason(string reason) => FlagReason = reason;

    internal void AddFinishedLines(IEnumerable<(short SizeId, short ColourId, int Qty)> lines) =>
        _finishedLines.AddRange(lines.Select(l => new FinishedQuantityLine(l.SizeId, l.ColourId, l.Qty)));
}

/// <summary>Maps to PROD.PROD_MLST_FNSH_LINE (D8): finished / ready-to-ship quantity per size x colour. Gates nothing.</summary>
public sealed class FinishedQuantityLine : Entity<long>, IAuditable
{
    private FinishedQuantityLine()
    {
    }

    internal FinishedQuantityLine(short sizeId, short colourId, int qty)
    {
        SizeId = sizeId;
        ColourId = colourId;
        Qty = qty;
    }

    public long MilestoneId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int Qty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
