namespace Romp.Modules.Warehouse.Contracts;

/// <summary>One variant and how many units of it an order needs. <c>VariantId</c> is Labelling's variant id, as in the stock events.</summary>
public sealed record AllocationLineRequest(long VariantId, int Quantity);

/// <summary>
/// Units of one variant set aside in one bin for an order (ALC-3, ALC-4). <see cref="WalkKey"/> sorts bins in walking order
/// (zone, aisle, shelf). Nothing here is personal data.
/// </summary>
public sealed record AllocatedUnit(long VariantId, long BinId, string BinCode, string WalkKey, int Quantity, string Gtin, string Sku);

/// <summary>A variant that could not be covered in full: how many were wanted and how many bin stock could give (ALC-8).</summary>
public sealed record ShortAllocation(long VariantId, int Requested, int Allocated);

/// <summary>
/// What an allocation holds after a call. <see cref="Allocations"/> lists every bin claim for the order's requested variants (earlier calls
/// included); <see cref="Shortages"/> lists what is still missing. <see cref="IsClosed"/> is true when the order's allocation was released
/// or already picked, so nothing was changed.
/// </summary>
public sealed record AllocationResult(IReadOnlyList<AllocatedUnit> Allocations, IReadOnlyList<ShortAllocation> Shortages, bool IsClosed = false);

/// <summary>Units of one variant taken from one bin (the picker's confirmed scans).</summary>
public sealed record PickedUnit(long VariantId, long BinId, int Quantity);

/// <summary>
/// How Fulfilment gets bin-located stock for an order and turns picked units into a real decrement (ADR 0030). Warehouse owns bins and
/// balances, so allocation, release and the pick decrement all happen here. On-hand stock does not change until
/// <see cref="ConfirmPickAsync"/> (ALC-10). Every call is idempotent on the order key.
/// </summary>
public interface IStockAllocator
{
    /// <summary>The most different variants one allocation may hold.</summary>
    public const int MaxLines = 100;

    /// <summary>
    /// Allocates each line to bins, oldest putaway first, under row locks taken in a fixed order (ALC-3, 4, 9). Asking again tops up what was
    /// short (after a restock) and changes nothing that is already covered (ALC-5). A shortage is reported, never thrown (ALC-8).
    /// </summary>
    Task<AllocationResult> AllocateAsync(string orderKey, IReadOnlyList<AllocationLineRequest> lines, CancellationToken cancellationToken);

    /// <summary>Drops the allocation so its units can be allocated to other orders (ALC-6). Unknown, released or picked keys do nothing.</summary>
    Task ReleaseAsync(string orderKey, CancellationToken cancellationToken);

    /// <summary>
    /// Decrements each picked unit from its bin in the stock ledger exactly once and converts the order's reservation (PCK-7, RSV-9).
    /// A repeat for an allocation that is already picked changes nothing (PCK-8).
    /// </summary>
    Task ConfirmPickAsync(string orderKey, IReadOnlyList<PickedUnit> picks, string staff, CancellationToken cancellationToken);

    /// <summary>
    /// Gives up part of a bin claim: a picker found fewer units than the system claimed there (PEX-1). The units already picked stay claimed; the
    /// rest become allocatable elsewhere. Asking for more than is claimed gives up what there is.
    /// </summary>
    Task ShortenClaimAsync(string orderKey, long variantId, long binId, int quantity, CancellationToken cancellationToken);

    /// <summary>Marks a bin "under review": it is skipped by allocation until cleared (PEX-6). Stock is never overwritten.</summary>
    Task FlagBinAsync(long binId, string reasonCode, CancellationToken cancellationToken);

    /// <summary>Clears the "under review" mark once staff have counted or adjusted the bin.</summary>
    Task ClearBinFlagAsync(long binId, CancellationToken cancellationToken);
}
