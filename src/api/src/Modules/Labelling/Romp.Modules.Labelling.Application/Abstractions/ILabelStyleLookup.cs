namespace Romp.Modules.Labelling.Application.Abstractions;

/// <summary>A style as labelling sees it: just what a tag and a SKU need (read through Catalog's contract).</summary>
public sealed record LabelStyle(
    long Id,
    string Code,
    string Name,
    decimal TargetRetailPrice,
    DateTimeOffset TargetPriceChangedAt,
    bool IsActive);

/// <summary>A colour or size as labelling sees it (read through Reference's contract).</summary>
public sealed record LabelCode(short Id, string Code, string Name);

/// <summary>
/// Everything Labelling needs from other modules, behind one port (ADR 0002, ADR 0011): the PO's single style (D11), style data,
/// and colour and size codes. Implemented in Infrastructure over the other modules' <c>*.Contracts</c>; each call is one query
/// however many ids are asked for, so lists never run per row.
/// </summary>
public interface ILabelStyleLookup
{
    /// <summary>The style a PO is for, or null when the PO is unknown or has no sent terms yet.</summary>
    Task<long?> FindStyleIdForPoAsync(long poId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<long, LabelStyle>> GetStylesAsync(IReadOnlyCollection<long> styleIds, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<short, LabelCode>> GetColoursAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<short, LabelCode>> GetSizesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken);
}
