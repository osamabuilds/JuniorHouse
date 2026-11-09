using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Warehouse.Domain.Allocations;

/// <summary>WHSE.ALOC_STS_LKP</summary>
public static class AllocationStatuses
{
    public const short Active = 1;
    public const short Picked = 2;
    public const short Released = 3;

    public static bool IsKnown(short id) => id is Active or Picked or Released;
}

/// <summary>WHSE.ALOC_STS_LKP</summary>
public sealed class AllocationStatusLookup : WarehouseLookup;

/// <summary>Units of one variant and grade set aside in one bin for an order (WHSE.STCK_ALOC_LINE).</summary>
public sealed class AllocationLine : Entity<long>, IAuditable
{
    private AllocationLine()
    {
        InsrBy = string.Empty;
    }

    internal AllocationLine(long variantRefId, short gradeId, long binId, int quantity)
    {
        VariantRefId = variantRefId;
        GradeId = gradeId;
        BinId = binId;
        Quantity = quantity;
        InsrBy = string.Empty;
    }

    public long AllocationId { get; private set; }

    public long VariantRefId { get; private set; }

    public short GradeId { get; private set; }

    public long BinId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    internal void Add(int quantity) => Quantity += quantity;
}

/// <summary>
/// The bin-level claim of one order (WHSE.STCK_ALOC), found by the order key, which is unique: allocating twice for one key is the same
/// allocation (ALC-5). It moves <c>Active → Picked</c> or <c>Active → Released</c>; a repeat of either step changes nothing. Only an active
/// allocation holds units back from other orders; on-hand balances are untouched until the pick is confirmed (ALC-10).
/// </summary>
public sealed class StockAllocation : AggregateRoot<long>, IAuditable
{
    public const int MaxKeyLength = 64;

    private readonly List<AllocationLine> _lines = [];

    private StockAllocation()
    {
        OrderKey = string.Empty;
        InsrBy = string.Empty;
    }

    public StockAllocation(string orderKey)
    {
        if (string.IsNullOrWhiteSpace(orderKey) || orderKey.Length > MaxKeyLength)
        {
            throw new DomainException($"An allocation needs an order key of up to {MaxKeyLength} characters.");
        }

        OrderKey = orderKey;
        InsrBy = string.Empty;
    }

    public string OrderKey { get; private set; }

    public short StatusId { get; private set; } = AllocationStatuses.Active;

    public IReadOnlyList<AllocationLine> Lines => _lines;

    public bool IsActive => StatusId == AllocationStatuses.Active;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Sets aside units in a bin. Another claim in the same bin and grade is merged into one row.</summary>
    public void Claim(long variantRefId, short gradeId, long binId, int quantity)
    {
        if (!IsActive)
        {
            throw new DomainException("A released or picked allocation cannot take more stock.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("A claim is for at least one unit.");
        }

        var existing = _lines.FirstOrDefault(l => l.VariantRefId == variantRefId && l.GradeId == gradeId && l.BinId == binId);
        if (existing is null)
        {
            _lines.Add(new AllocationLine(variantRefId, gradeId, binId, quantity));
        }
        else
        {
            existing.Add(quantity);
        }
    }

    /// <summary>Gives up units of a claim in one bin; the row goes when nothing is left. Returns how many were given up.</summary>
    public int Shorten(long variantRefId, long binId, int quantity)
    {
        if (!IsActive || quantity <= 0)
        {
            return 0;
        }

        var remaining = quantity;
        foreach (var line in _lines.Where(l => l.VariantRefId == variantRefId && l.BinId == binId).OrderBy(l => l.GradeId).ToList())
        {
            var take = Math.Min(remaining, line.Quantity);
            line.Add(-take);
            remaining -= take;
            if (line.Quantity == 0)
            {
                _lines.Remove(line);
            }
        }

        return quantity - remaining;
    }

    public int ClaimedFor(long variantRefId) => _lines.Where(l => l.VariantRefId == variantRefId).Sum(l => l.Quantity);

    /// <summary>The allocation is given back. A repeat is a no-op; a picked allocation cannot be released.</summary>
    public bool Release()
    {
        if (StatusId != AllocationStatuses.Active)
        {
            return false;
        }

        StatusId = AllocationStatuses.Released;
        return true;
    }

    /// <summary>The pick was confirmed and the ledger decremented. A repeat is a no-op.</summary>
    public bool MarkPicked()
    {
        if (StatusId != AllocationStatuses.Active)
        {
            return false;
        }

        StatusId = AllocationStatuses.Picked;
        return true;
    }
}
