using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Application.StockLedger;

/// <summary>What changed for one post: the balance of that variant, grade and bin before and after.</summary>
public sealed record PostedMovement(StockPost Post, int BalanceBefore, int BalanceAfter);

/// <summary>
/// The only way stock changes (ADR 0012). Runs inside the caller's transaction: for each post it atomically changes the bin balance
/// (a negative change that would go below zero throws a <c>DomainException</c> with <see cref="StockMessages.Insufficient"/> and
/// the caller's transaction rolls back), appends one ledger row with before and after, and raises one <c>StockLevelChangedEvent</c>
/// per touched variant. Never takes a total from the caller (LED-10).
/// </summary>
public interface IStockPosting
{
    Task<IReadOnlyList<PostedMovement>> PostAsync(IReadOnlyList<StockPost> posts, string actingPerson, CancellationToken cancellationToken);
}

/// <summary>A variant as the other warehouse features see it (read from warehouse's own reference, never from Labelling's tables).</summary>
public sealed record StockVariantInfo(
    long Id, long VariantId, long StyleId, short ColourId, short SizeId, string SkuCode, string Gtin, bool GtinRetired);

/// <summary>Stock-ledger's public read surface for putaway, adjustments, counts and alerts.</summary>
public interface IStockVariantReader
{
    Task<StockVariantInfo?> FindByGtinAsync(string gtin, CancellationToken cancellationToken);

    Task<StockVariantInfo?> FindByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>Finds a variant by Labelling's variant id (what events carry).</summary>
    Task<StockVariantInfo?> FindByVariantIdAsync(long variantId, CancellationToken cancellationToken);

    Task<StockVariantInfo?> FindByStyleColourSizeAsync(long styleId, short colourId, short sizeId, CancellationToken cancellationToken);

    /// <summary>Total on hand across all bins and grades (available equals on-hand until reservations exist).</summary>
    Task<int> GetOnHandAsync(long variantRefId, CancellationToken cancellationToken);

    /// <summary>True when the variant has ever held stock (used by alerts so a never-stocked variant raises none, ALR-4).</summary>
    Task<bool> HasEverHeldStockAsync(long variantRefId, CancellationToken cancellationToken);
}

public sealed record StockBinQuantityDto(long BinId, string BinCode, short GradeId, string GradeCode, int Quantity);

public sealed record StockVariantRowDto(
    long VariantRefId,
    long VariantId,
    string SkuCode,
    string Gtin,
    bool GtinRetired,
    int OnHand,
    int Available,
    int OnHandAccepted,
    int OnHandConcession,
    IReadOnlyList<StockBinQuantityDto> Bins);

public sealed record BinContentDto(long VariantRefId, string SkuCode, string Gtin, short GradeId, string GradeCode, int Quantity);

public sealed record StockMovementDto(
    long Id,
    long VariantRefId,
    string SkuCode,
    long BinId,
    string BinCode,
    string GradeCode,
    string KindCode,
    long ReferenceId,
    int QuantityChange,
    int BalanceBefore,
    int BalanceAfter,
    string ActorName,
    DateTimeOffset At);

public sealed record ConsistencyMismatchDto(long VariantRefId, short GradeId, long BinId, int Ledger, int Balance);
