namespace Romp.Modules.Quality.Domain.Receiving;

/// <summary>One size and colour: what the vendor claimed against what Romp counted.</summary>
public sealed record ReconciliationLine(short SizeId, short ColourId, int ClaimedQty, int CountedQty)
{
    /// <summary>Counted minus claimed: negative is short, positive is over.</summary>
    public int DiffQty => CountedQty - ClaimedQty;

    public bool IsExcess => DiffQty > 0;
}

/// <summary>The judged receipt: the overall status (a <see cref="GrnReconciliation"/> id) and the totals.</summary>
public sealed record ReconciliationResult(short StatusId, int ClaimedTotal, int CountedTotal, IReadOnlyList<ReconciliationLine> Lines);

/// <summary>
/// Pure rule for the reconciliation status (spec AC-7): <c>Matched</c> when every line's count equals its claim.
/// Otherwise <c>Short</c> when some line is short and none is over, <c>Over</c> when some line is over and none is short.
/// When lines differ in both directions the totals decide (fewer counted than claimed is Short, more is Over), and an
/// equal total with mixed lines is <c>Short</c>, the conservative reading, because some line has not arrived.
/// A discrepancy never blocks a receipt.
/// </summary>
public static class ReconciliationJudge
{
    public static ReconciliationResult Judge(IReadOnlyList<ReconciliationLine> lines)
    {
        var claimedTotal = lines.Sum(l => l.ClaimedQty);
        var countedTotal = lines.Sum(l => l.CountedQty);
        var anyShort = lines.Any(l => l.DiffQty < 0);
        var anyOver = lines.Any(l => l.DiffQty > 0);

        short status;
        if (!anyShort && !anyOver)
        {
            status = GrnReconciliation.Matched;
        }
        else if (anyShort && !anyOver)
        {
            status = GrnReconciliation.Shortage;
        }
        else if (anyOver && !anyShort)
        {
            status = GrnReconciliation.Over;
        }
        else
        {
            status = countedTotal > claimedTotal ? GrnReconciliation.Over : GrnReconciliation.Shortage;
        }

        return new ReconciliationResult(status, claimedTotal, countedTotal, lines);
    }
}
