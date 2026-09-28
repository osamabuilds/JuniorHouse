namespace Romp.Modules.Vendor.Contracts;

/// <summary>
/// The only way another module (CTLG, for the style-edit guard - AC-2, AC-3) may read VNDR
/// data, per ADR 0002 ("no module queries another module's tables"). Implemented in
/// Romp.Modules.Vendor.Infrastructure, registered against this interface so CTLG depends only on
/// this Contracts project, never on Vendor's Domain/Application/Infrastructure.
/// </summary>
public interface IPurchaseOrderUsageQueries
{
    /// <summary>
    /// One row per size x colour cell a non-cancelled PO's lines still use for this style (task
    /// 28 will extend this to also cover a Pending revision's lines once PO_REV_LINE exists).
    /// </summary>
    Task<IReadOnlyCollection<PoSizeColourUsage>> GetActiveSizeColourUsageAsync(
        long styleId, CancellationToken cancellationToken);
}

/// <summary>One non-cancelled PO's use of one size x colour cell of a style (AC-2).</summary>
public sealed record PoSizeColourUsage(short SizeId, short ColourId, string PoNo);
