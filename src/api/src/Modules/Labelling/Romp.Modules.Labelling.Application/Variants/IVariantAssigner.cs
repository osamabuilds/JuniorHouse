namespace Romp.Modules.Labelling.Application.Variants;

/// <summary>One variant a caller needs a GTIN for, with the codes its SKU is built from.</summary>
public sealed record VariantRequest(long StyleId, string StyleCode, short ColourId, string ColourCode, short SizeId, string SizeCode);

/// <summary>
/// The public surface of <c>variants</c> for other features (loose coupling inside the module): make sure every requested
/// variant exists and has an active GTIN, assigning the next number lazily (D9). Runs in the caller's transaction.
/// </summary>
public interface IVariantAssigner
{
    /// <summary>Returns the variant id for each requested (style, colour, size).</summary>
    Task<IReadOnlyDictionary<(long StyleId, short ColourId, short SizeId), long>> EnsureGtinsAsync(
        IReadOnlyCollection<VariantRequest> requests, CancellationToken cancellationToken);
}

/// <summary>What other features may know about a variant's current identity.</summary>
public sealed record VariantSnapshot(
    long VariantId,
    long StyleId,
    short ColourId,
    short SizeId,
    string SkuCode,
    long? ActiveGtinId,
    string? ActiveGtin);

/// <summary>The second public surface of <c>variants</c>: read a variant's SKU and active GTIN.</summary>
public interface IVariantReader
{
    Task<VariantSnapshot?> GetByVariantIdAsync(long variantId, CancellationToken cancellationToken);

    /// <summary>By the id of a GTIN row (which may be retired), so a job that was queued keeps resolving.</summary>
    Task<VariantSnapshot?> GetByGtinIdAsync(long gtinId, CancellationToken cancellationToken);
}
