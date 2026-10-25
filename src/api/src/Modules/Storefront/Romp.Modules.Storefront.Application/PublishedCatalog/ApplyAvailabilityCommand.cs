using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Application.PublishedCatalog;

public enum AvailabilityOutcome
{
    /// <summary>The report was not newer than the one held (a repeat or an older one), or Warehouse has no stock row for the variant.</summary>
    Unchanged = 0,

    /// <summary>Stored, but no published variant has this SKU yet; it waits in the holding area until the product arrives (PCT-5).</summary>
    Held = 1,

    /// <summary>Stored and applied to a published variant, whose product's flags were recomputed.</summary>
    Applied = 2,
}

/// <summary>
/// Projects what Warehouse reports for one variant (PCT-3, 5, 9, 11). The report is the current state, fetched by the caller; a null
/// report means the variant has never had a stock posting and changes nothing (the variant stays "not yet available"). The report is
/// kept in <c>AVLB_STK</c> whether or not its product is known, because a stock message can arrive before the product does.
/// </summary>
public sealed record ApplyAvailabilityCommand(StockAvailability? Availability) : IRequest<AvailabilityOutcome>, IStorefrontCommand;

public sealed class ApplyAvailabilityCommandHandler(IStorefrontReadDbContext db, IReadModelSettings settings)
    : IRequestHandler<ApplyAvailabilityCommand, AvailabilityOutcome>
{
    public async Task<AvailabilityOutcome> Handle(ApplyAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var report = request.Availability;
        if (report is null)
        {
            return AvailabilityOutcome.Unchanged;
        }

        var held = await db.Stock.FirstOrDefaultAsync(s => s.Id == report.VariantId, cancellationToken);
        if (held is null)
        {
            db.Stock.Add(new StockRow(report.VariantId, report.SkuCode, report.Available, report.Version));
        }
        else if (!held.Apply(report.SkuCode, report.Available, report.Version))
        {
            return AvailabilityOutcome.Unchanged;
        }

        var sku = VariantRow.NormaliseSku(report.SkuCode);
        var variant = sku is null ? null : await db.Variants.FirstOrDefaultAsync(v => v.SkuNormalised == sku, cancellationToken);
        if (variant is null)
        {
            return AvailabilityOutcome.Held;
        }

        variant.ApplyStock(report.Available, report.Version);
        var product = await db.Products.SingleAsync(p => p.Id == variant.ProductId, cancellationToken);
        await ReadModelProjector.RecomputeAsync(db, product, settings, cancellationToken);
        return AvailabilityOutcome.Applied;
    }
}
