using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Warehouse.Domain.CycleCounts;

/// <summary>WHSE.CNT_STS_LKP</summary>
public static class CountStatuses
{
    public const short Open = 1;
    public const short Closed = 2;
    public const short Cancelled = 3;
}

/// <summary>WHSE.CNT_STS_LKP</summary>
public sealed class CountStatusLookup : WarehouseLookup;

/// <summary>One bin included in a count. <see cref="OpenMovementMark"/> is the newest ledger movement of that bin when the count was created (CC-7).</summary>
public sealed class CountBin : Entity<long>, IAuditable
{
    private CountBin()
    {
    }

    internal CountBin(long binId, long openMovementMark)
    {
        BinId = binId;
        OpenMovementMark = openMovementMark;
        IsOpen = true;
    }

    public long CountId { get; private set; }

    public long BinId { get; private set; }

    public long OpenMovementMark { get; private set; }

    /// <summary>True while the parent count is Open. A partial unique index allows one open count per bin (CC-3).</summary>
    public bool IsOpen { get; internal set; }

    /// <summary>True once counts have been submitted for this bin (an empty submission means "counted, found nothing").</summary>
    public bool Submitted { get; internal set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    internal void RefreshMark(long mark) => OpenMovementMark = mark;

    internal void MarkSubmitted() => Submitted = true;
}

/// <summary>What the counter found: this variant, grade and bin held this many units. System quantity is filled in only when the count is confirmed.</summary>
public sealed class CountLine : Entity<long>, IAuditable
{
    private CountLine()
    {
    }

    internal CountLine(long binId, long variantRefId, short gradeId, int countedQuantity)
    {
        BinId = binId;
        VariantRefId = variantRefId;
        GradeId = gradeId;
        CountedQuantity = countedQuantity;
    }

    public long CountId { get; private set; }

    public long BinId { get; private set; }

    public long VariantRefId { get; private set; }

    public short GradeId { get; private set; }

    public int CountedQuantity { get; private set; }

    public int? SystemQuantity { get; private set; }

    public short? ReasonId { get; private set; }

    public long? AdjustmentId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    internal void SetCounted(int quantity) => CountedQuantity = quantity;

    public void Resolve(int systemQuantity, short? reasonId, long? adjustmentId)
    {
        SystemQuantity = systemQuantity;
        ReasonId = reasonId;
        AdjustmentId = adjustmentId;
    }
}

/// <summary>
/// A partial physical recount of chosen bins (BRD §5.14, WHSE.CNT_MAIN). Counts are blind: system quantities are not part of what the
/// counter sees (CC-2). Every variance becomes a reason-coded adjustment on confirm, never a silent overwrite (CC-5).
/// </summary>
public sealed class CycleCount : AggregateRoot<long>, IAuditable
{
    private readonly List<CountBin> _bins = [];
    private readonly List<CountLine> _lines = [];

    private CycleCount()
    {
        CountNo = string.Empty;
        CreatedBy = string.Empty;
    }

    public CycleCount(string countNo, IReadOnlyDictionary<long, long> binMarks, string createdBy)
    {
        if (binMarks.Count is 0 or > 200)
        {
            throw new DomainException("A count covers between 1 and 200 bins.");
        }

        CountNo = countNo;
        StatusId = CountStatuses.Open;
        CreatedBy = createdBy;
        _bins.AddRange(binMarks.Select(p => new CountBin(p.Key, p.Value)));
    }

    public string CountNo { get; private set; }

    public short StatusId { get; private set; }

    public string CreatedBy { get; private set; }

    public IReadOnlyCollection<CountBin> Bins => _bins.AsReadOnly();

    public IReadOnlyCollection<CountLine> Lines => _lines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public bool IsOpen => StatusId == CountStatuses.Open;

    /// <summary>
    /// Records what was counted for one bin, replacing that bin's earlier lines (CC-6). With <paramref name="recount"/> the bin's
    /// staleness mark moves to <paramref name="currentMark"/>, which is how a stale bin is recounted (CC-7).
    /// </summary>
    public void SetBinCounts(long binId, IReadOnlyCollection<(long VariantRefId, short GradeId, int Quantity)> counts, bool recount, long currentMark)
    {
        EnsureOpen();
        var bin = _bins.FirstOrDefault(b => b.BinId == binId) ?? throw new DomainException("That bin is not part of this count.");

        if (counts.Count > 2_000 || counts.Any(c => c.Quantity is < 0 or > 100_000))
        {
            throw new DomainException("Counted quantities must be whole numbers from 0 to 100,000, at most 2,000 lines.");
        }

        if (counts.GroupBy(c => (c.VariantRefId, c.GradeId)).Any(g => g.Count() > 1))
        {
            throw new DomainException("A variant and grade can appear only once per bin; add the quantities together.");
        }

        bin.MarkSubmitted();
        _lines.RemoveAll(l => l.BinId == binId);
        _lines.AddRange(counts.Select(c => new CountLine(binId, c.VariantRefId, c.GradeId, c.Quantity)));
        if (recount)
        {
            bin.RefreshMark(currentMark);
        }
    }

    /// <summary>The line for a variant and grade in a bin, added with a counted quantity of zero when the counter never scanned it (it is missing from the shelf).</summary>
    public CountLine EnsureLine(long binId, long variantRefId, short gradeId)
    {
        var line = _lines.FirstOrDefault(l => l.BinId == binId && l.VariantRefId == variantRefId && l.GradeId == gradeId);
        if (line is null)
        {
            line = new CountLine(binId, variantRefId, gradeId, 0);
            _lines.Add(line);
        }

        return line;
    }

    public void Close()
    {
        EnsureOpen();
        StatusId = CountStatuses.Closed;
        _bins.ForEach(b => b.IsOpen = false);
    }

    public void Cancel()
    {
        EnsureOpen();
        StatusId = CountStatuses.Cancelled;
        _bins.ForEach(b => b.IsOpen = false);
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new DomainException($"Count {CountNo} is no longer open.");
        }
    }
}

/// <summary>WHSE.CNT_NO_SEQ: the per-year counter behind <c>CC-{YYYY}-{NNNN}</c> (ADR 0006).</summary>
public sealed class CountNumberSequence
{
    public short Year { get; private set; }

    public int Seq { get; private set; }
}
