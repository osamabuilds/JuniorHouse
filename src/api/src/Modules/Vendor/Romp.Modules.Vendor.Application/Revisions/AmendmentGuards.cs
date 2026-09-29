using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Vendor.Domain.PurchaseOrders;

namespace Romp.Modules.Vendor.Application.Revisions;

/// <summary>
/// Sprint 3 amendment rules that need production facts (spec AC-26, AC-27, ADR 0008). They use
/// <see cref="IProductionQueries"/> (a consistent read at the moment of the decision), not an
/// event-fed copy that could lag behind a delivery note.
/// </summary>
internal static class AmendmentGuards
{
    /// <summary>AC-26: a PO that is Delivered or Closed can no longer be amended (PoStatus ids 7, 8).</summary>
    public static void EnsureNotDelivered(PurchaseOrder po)
    {
        if (po.StatusId is 7 or 8)
        {
            throw new DomainException($"PO {po.PoNo} is already Delivered or Closed and can no longer be amended.");
        }
    }

    /// <summary>AC-26: no line may go below what has already been delivered.</summary>
    public static async Task EnsureNotBelowDeliveredAsync(
        IProductionQueries productionQueries,
        PurchaseOrder po,
        IEnumerable<(short SizeId, short ColourId, int Qty)> proposedLines,
        CancellationToken cancellationToken)
    {
        var delivered = await productionQueries.GetDeliveredQuantitiesAsync(po.Id, cancellationToken);
        if (delivered.Count == 0)
        {
            return;
        }

        var proposed = proposedLines.ToDictionary(l => (l.SizeId, l.ColourId), l => l.Qty);
        var below = delivered
            .Where(d => d.Qty > proposed.GetValueOrDefault((d.SizeId, d.ColourId)))
            .ToList();

        if (below.Count > 0)
        {
            var worst = below[0];
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["Lines"] = [$"{below.Count} line(s) would go below what the vendor has already delivered on PO {po.PoNo} (for example {worst.Qty} pieces are already delivered against {proposed.GetValueOrDefault((worst.SizeId, worst.ColourId))} ordered). Delivered quantities cannot be amended away."],
            });
        }
    }
}
