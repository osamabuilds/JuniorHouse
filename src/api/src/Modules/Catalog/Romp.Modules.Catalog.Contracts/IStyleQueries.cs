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
}

/// <summary>Just enough of a style for VNDR to validate a PO's style reference and size/colour lines against (AC-7, AC-8).</summary>
public sealed record StyleSummary(
    long Id,
    string Code,
    string Name,
    IReadOnlyCollection<short> SizeIds,
    IReadOnlyCollection<short> ColourIds);
