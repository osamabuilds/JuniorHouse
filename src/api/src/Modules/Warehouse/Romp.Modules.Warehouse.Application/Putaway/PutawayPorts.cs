namespace Romp.Modules.Warehouse.Application.Putaway;

/// <summary>
/// The one thing putaway needs from Vendor: the style a PO is for (one style per PO, labelling D11). Implemented in Infrastructure
/// over <c>Vendor.Contracts</c>, so putaway never sees Vendor's tables or types.
/// </summary>
public interface IPoStyleLookup
{
    /// <summary>The style of the PO, or null when the PO is unknown or has no sent terms yet.</summary>
    Task<long?> FindStyleIdForPoAsync(long poId, CancellationToken cancellationToken);
}

/// <summary>Row locks (<c>SELECT … FOR UPDATE</c>) that serialise concurrent confirmations of one putaway item (PUT-9).</summary>
public interface IPutawayRowLocks
{
    Task LockItemAsync(long itemId, CancellationToken cancellationToken);
}

public sealed record PutawayItemRowDto(
    long Id,
    long SubBatchId,
    long PoId,
    string? SkuCode,
    string? Gtin,
    bool GtinRetired,
    short GradeId,
    string GradeCode,
    int QcQuantity,
    int PutQuantity,
    int Pending,
    short StatusId,
    string StatusCode,
    DateTimeOffset ReceivedAt);

public sealed record ScanPendingItemDto(long ItemId, long PoId, short GradeId, string GradeCode, int Pending);

/// <summary>What a GTIN scan answers (PUT-6, PUT-7): the variant, whether its barcode is retired, and the items still waiting to be shelved.</summary>
public sealed record ScanLookupDto(long VariantRefId, string SkuCode, string Gtin, bool GtinRetired, IReadOnlyList<ScanPendingItemDto> PendingItems);

public sealed record PutawayRecordDto(
    long Id,
    long ItemId,
    long BinId,
    string BinCode,
    int Quantity,
    bool Scanned,
    string ActorName,
    DateTimeOffset At,
    int ItemPending,
    string? SkuCode);
