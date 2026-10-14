namespace Romp.Modules.Catalog.Contracts;

/// <summary>
/// The only way another module (VNDR, for purchase-order line validation - AC-8) may read CTLG
/// data, per ADR 0002 ("no module queries another module's tables"). Implemented in
/// Romp.Modules.Catalog.Infrastructure, registered against this interface so VNDR depends only on
/// this Contracts project, never on Catalog's Domain/Application/Infrastructure.
/// </summary>
public interface IStyleQueries
{
    Task<StyleSummary?> FindActiveStyleAsync(long styleId, CancellationToken cancellationToken);

    /// <summary>One query for any number of styles (no N+1). Unknown ids are absent from the result.</summary>
    Task<IReadOnlyDictionary<long, StyleLabelInfo>> GetLabelInfoAsync(IReadOnlyCollection<long> styleIds, CancellationToken cancellationToken);
}

/// <summary>Just enough of a style for VNDR to validate a PO's style reference and size/colour lines against (AC-7, AC-8).</summary>
public sealed record StyleSummary(
    long Id,
    string Code,
    string Name,
    IReadOnlyCollection<short> SizeIds,
    IReadOnlyCollection<short> ColourIds);

/// <summary>
/// Sprint 5 (labelling): what the tag-printing feature may read about a style. Retired styles are included (with
/// <see cref="IsActive"/> false) because stock already received still has to be labelled. <see cref="TargetPriceChangedAt"/>
/// is the last time the target retail price changed (the style's creation time until it first changes).
/// </summary>
public sealed record StyleLabelInfo(
    long Id,
    string Code,
    string Name,
    decimal TargetRetailPrice,
    DateTimeOffset TargetPriceChangedAt,
    bool IsActive);
