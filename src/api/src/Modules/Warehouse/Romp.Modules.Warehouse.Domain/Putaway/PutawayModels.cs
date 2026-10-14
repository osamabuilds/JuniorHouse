using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Warehouse.Domain.Putaway;

/// <summary>WHSE.PUT_STS_LKP</summary>
public static class PutawayStatuses
{
    public const short AwaitingGtin = 1;
    public const short Pending = 2;
    public const short Complete = 3;

    public static bool IsKnown(short id) => id is AwaitingGtin or Pending or Complete;
}

/// <summary>WHSE.PUT_STS_LKP</summary>
public sealed class PutawayStatusLookup : WarehouseLookup;

/// <summary>
/// One accepted line of one sub-batch waiting to be shelved (WHSE.PUT_ITEM): how many units QC released, how many are put away,
/// and the variant they are for once Labelling has assigned its GTIN. The pending quantity is derived, never stored (PUT-1).
/// Unique on (sub-batch, size, colour), so a redelivered QC message creates nothing new (PUT-2).
/// </summary>
public sealed class PutawayItem : AggregateRoot<long>, IAuditable
{
    private PutawayItem()
    {
    }

    public PutawayItem(long subBatchId, long qcBatchId, long poId, long styleId, short colourId, short sizeId, short gradeId, int qcQuantity, long? variantRefId = null)
    {
        if (qcQuantity <= 0)
        {
            throw new DomainException("A putaway item needs a positive accepted quantity.");
        }

        if (!Grades.IsKnown(gradeId))
        {
            throw new DomainException($"Unknown stock grade {gradeId}.");
        }

        SubBatchId = subBatchId;
        QcBatchId = qcBatchId;
        PoId = poId;
        StyleId = styleId;
        ColourId = colourId;
        SizeId = sizeId;
        GradeId = gradeId;
        QcQuantity = qcQuantity;
        VariantRefId = variantRefId;
        StatusId = variantRefId is null ? PutawayStatuses.AwaitingGtin : PutawayStatuses.Pending;
    }

    public long SubBatchId { get; private set; }

    public long QcBatchId { get; private set; }

    public long PoId { get; private set; }

    public long StyleId { get; private set; }

    public short ColourId { get; private set; }

    public short SizeId { get; private set; }

    public short GradeId { get; private set; }

    /// <summary>What QC released for this line. Rejected, rework and undecided quantity never arrives here (PUT-12).</summary>
    public int QcQuantity { get; private set; }

    public int PutQuantity { get; private set; }

    public long? VariantRefId { get; private set; }

    public short StatusId { get; private set; }

    public int Pending => QcQuantity - PutQuantity;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>The QC outcome name to a stock grade; null for any outcome that is not released (only Accepted and Concession are).</summary>
    public static short? GradeFromOutcome(string? outcome) => outcome switch
    {
        "Accepted" => Grades.Accepted,
        "Concession" => Grades.Concession,
        _ => null,
    };

    /// <summary>Labelling has assigned this item's variant a GTIN: it can now be scanned and put away. Linking twice changes nothing.</summary>
    public void LinkVariant(long variantRefId)
    {
        VariantRefId ??= variantRefId;
        if (StatusId == PutawayStatuses.AwaitingGtin)
        {
            StatusId = Pending == 0 ? PutawayStatuses.Complete : PutawayStatuses.Pending;
        }
    }

    /// <summary>Shelves <paramref name="quantity"/> units. More than is pending is refused, with the way forward (PUT-5, PUT-9, PUT-21).</summary>
    public void Record(int quantity)
    {
        if (VariantRefId is null)
        {
            throw new DomainException("This item is still waiting for its GTIN, so it cannot be put away yet.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("The quantity to put away must be a whole number above zero.");
        }

        if (quantity > Pending)
        {
            throw new DomainException(
                $"Only {Pending} unit(s) are pending for this item. Units found beyond what QC accepted are recorded as a FOUND_STOCK adjustment.");
        }

        PutQuantity += quantity;
        StatusId = Pending == 0 ? PutawayStatuses.Complete : PutawayStatuses.Pending;
    }
}

/// <summary>One confirmed putaway (WHSE.PUT_RCRD): immutable. A mistake is corrected by an adjustment, never by editing this (PUT-13).</summary>
public sealed class PutawayRecord : Entity<long>, IAuditable
{
    private PutawayRecord()
    {
        ActorName = string.Empty;
    }

    public PutawayRecord(long itemId, long binId, int quantity, bool scanned, Guid idempotencyKey, string actorName)
    {
        if (quantity <= 0)
        {
            throw new DomainException("A putaway record needs a positive quantity.");
        }

        ItemId = itemId;
        BinId = binId;
        Quantity = quantity;
        Scanned = scanned;
        IdempotencyKey = idempotencyKey;
        ActorName = actorName;
    }

    public long ItemId { get; private set; }

    public long BinId { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>True when the GTIN was scanned; false when the item was chosen from the list (nothing to scan yet, PUT-19).</summary>
    public bool Scanned { get; private set; }

    public Guid IdempotencyKey { get; private set; }

    public string ActorName { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
