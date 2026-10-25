using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Application.Abstractions;

/// <summary>
/// Application's port onto the storefront's read model (ADR 0018): the published products and everything a page or listing shows about
/// them. The projection is the only writer; every public query reads these sets and nothing else, never another module's tables.
/// </summary>
public interface IStorefrontReadDbContext
{
    DbSet<PublishedProduct> Products { get; }

    DbSet<VariantRow> Variants { get; }

    DbSet<ImageRow> Images { get; }

    DbSet<DerivativeRow> Derivatives { get; }

    DbSet<CompositionRow> Composition { get; }

    DbSet<CareRow> CareInstructions { get; }

    DbSet<CollectionRow> Collections { get; }

    DbSet<SizeChartRow> Charts { get; }

    DbSet<SizeChartLineRow> ChartLines { get; }

    DbSet<StockRow> Stock { get; }

    DbSet<RebuildRun> RebuildRuns { get; }

    DbSet<ModelMeta> ModelMeta { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
