using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Domain.Search;

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

    DbSet<SynonymGroup> SynonymGroups { get; }

    DbSet<SynonymTerm> SynonymTerms { get; }

    DbSet<SearchMiss> SearchMisses { get; }

    /// <summary>The PostgreSQL row version (<c>xmin</c>) of a loaded synonym group; staff quote it back on their next save.</summary>
    uint RowVersionOf(SynonymGroup group);

    /// <summary>Makes the next save of the group conditional on the row version the client loaded, and always writes the group row.</summary>
    void RequireRowVersion(SynonymGroup group, uint expectedRowVersion);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Forgets every tracked entity. A long job (reconcile, rebuild) calls it between items so the context does not grow with the catalogue.</summary>
    void ClearTracker();
}
