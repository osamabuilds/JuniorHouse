namespace Romp.Modules.Production.Domain.Deliveries;

/// <summary>PROD.DLVR_TMNG_LKP ids.</summary>
public static class DeliveryTiming
{
    public const short OnTime = 1;
    public const short Late = 2;
    public const short BeyondAcceptable = 3;

    public static string Code(short id) => id switch
    {
        OnTime => "OnTime",
        Late => "Late",
        BeyondAcceptable => "BeyondAcceptable",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown id."),
    };
}

/// <summary>PROD.DLVR_QTY_STS_LKP ids.</summary>
public static class DeliveryQuantityStatus
{
    public const short OnQuantity = 1;
    public const short Shortage = 2;
    public const short Over = 3;

    public static string Code(short id) => id switch
    {
        OnQuantity => "OnQuantity",
        Shortage => "Short",
        Over => "Over",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown id."),
    };
}

public sealed record OrderedLine(short SizeId, short ColourId, int Qty);

/// <summary>The PO terms judged against: the revision in force on the dispatch date (D12).</summary>
public sealed record DeliveryTerms(
    short RevisionNumber,
    DateOnly ExpectedDate,
    DateOnly? LatestAcceptableDate,
    decimal? OverTolerancePercent,
    decimal? UnderTolerancePercent,
    IReadOnlyList<OrderedLine> Lines);

public sealed record NoteLineInput(short SizeId, short ColourId, int Qty);

public sealed record JudgedLine(short SizeId, short ColourId, int Qty, int OverQty, int ShortQty);

public sealed record DeliveryJudgement(
    short TimingId,
    int DaysLate,
    short QuantityStatusId,
    int OverQty,
    int ShortQty,
    bool PoIsDelivered,
    IReadOnlyList<JudgedLine> Lines);

/// <summary>
/// Pure delivery judgement (spec AC-21, AC-22, AC-24; plan "DeliveryJudge"). Over is flagged and
/// never refused (D4); a short delivery only counts as Delivered with the final-shipment marker
/// (D3). Tolerances are whole-piece: <c>maxAllowed = floor(ordered x (1 + over%))</c>,
/// <c>minRequired = ceil(ordered x (1 - under%))</c>; a missing tolerance is 0%.
/// </summary>
public static class DeliveryJudge
{
    public static int MaxAllowed(int ordered, decimal? overPercent) =>
        (int)Math.Floor(ordered * (1m + ((overPercent ?? 0m) / 100m)));

    public static int MinRequired(int ordered, decimal? underPercent) =>
        (int)Math.Ceiling(ordered * (1m - ((underPercent ?? 0m) / 100m)));

    public static DeliveryJudgement Judge(
        DeliveryTerms terms,
        IReadOnlyDictionary<(short SizeId, short ColourId), int> shippedBefore,
        IReadOnlyList<NoteLineInput> noteLines,
        DateOnly dispatchDate,
        bool isFinalShipment)
    {
        var thisNote = noteLines
            .GroupBy(l => (l.SizeId, l.ColourId))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Qty));

        var judgedLines = new List<JudgedLine>();
        var overTotal = 0;
        var shortTotal = 0;
        var allAtOrAboveMinimum = true;

        foreach (var ordered in terms.Lines)
        {
            var cell = (ordered.SizeId, ordered.ColourId);
            var before = shippedBefore.GetValueOrDefault(cell);
            var inNote = thisNote.GetValueOrDefault(cell);
            var cumulative = before + inNote;

            var maxAllowed = MaxAllowed(ordered.Qty, terms.OverTolerancePercent);
            var minRequired = MinRequired(ordered.Qty, terms.UnderTolerancePercent);

            var beyondAllowed = Math.Max(0, cumulative - maxAllowed);
            var overForNote = Math.Min(inNote, beyondAllowed);
            var missing = Math.Max(0, minRequired - cumulative);

            if (cumulative < minRequired)
            {
                allAtOrAboveMinimum = false;
            }

            overTotal += overForNote;
            shortTotal += missing;

            if (inNote > 0)
            {
                judgedLines.Add(new JudgedLine(ordered.SizeId, ordered.ColourId, inNote, overForNote, isFinalShipment ? missing : 0));
            }
        }

        // A line the PO never ordered cannot reach here (the command refuses it), but stay total: count it all as over.
        foreach (var (cell, qty) in thisNote.Where(n => terms.Lines.All(o => (o.SizeId, o.ColourId) != n.Key)))
        {
            overTotal += qty;
            judgedLines.Add(new JudgedLine(cell.SizeId, cell.ColourId, qty, qty, 0));
        }

        var shortReported = isFinalShipment ? shortTotal : 0;

        var quantityStatus = overTotal > 0
            ? DeliveryQuantityStatus.Over
            : shortReported > 0 ? DeliveryQuantityStatus.Shortage : DeliveryQuantityStatus.OnQuantity;

        short timing;
        var daysLate = 0;
        if (terms.LatestAcceptableDate is { } latest && dispatchDate > latest)
        {
            timing = DeliveryTiming.BeyondAcceptable;
            daysLate = dispatchDate.DayNumber - terms.ExpectedDate.DayNumber;
        }
        else if (dispatchDate > terms.ExpectedDate)
        {
            timing = DeliveryTiming.Late;
            daysLate = dispatchDate.DayNumber - terms.ExpectedDate.DayNumber;
        }
        else
        {
            timing = DeliveryTiming.OnTime;
        }

        return new DeliveryJudgement(
            timing, daysLate, quantityStatus, overTotal, shortReported,
            PoIsDelivered: allAtOrAboveMinimum || isFinalShipment, judgedLines);
    }
}
