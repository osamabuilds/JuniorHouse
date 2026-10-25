using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Domain.CompositionCare;
using Romp.Modules.Catalog.Domain.ProductMedia;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Domain.SizeCharts;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Domain.Urls;

namespace Romp.Modules.Catalog.Application.Abstractions;

/// <summary>
/// The CTLG module's persistence abstraction (Dependency Inversion: Application owns this
/// interface, Infrastructure's <c>CatalogDbContext</c> implements it) - lets query/command
/// handlers live in Application without depending on the Infrastructure project (ADR 0002).
/// </summary>
public interface ICatalogDbContext
{
    DbSet<Style> Styles { get; }

    DbSet<Product> Products { get; }

    DbSet<PlannedVariant> Variants { get; }

    DbSet<LabelledVariant> LabelledVariants { get; }

    DbSet<PriceHistoryEntry> PriceHistory { get; }

    DbSet<PathHistoryEntry> PathHistory { get; }

    DbSet<ProductImage> Images { get; }

    DbSet<CompositionLine> CompositionLines { get; }

    DbSet<ProductCare> CareInstructions { get; }

    DbSet<SizeChart> Charts { get; }

    DbSet<SizeChartLine> ChartLines { get; }

    /// <summary>A handler calls this itself only when it needs the DB-generated Style.Id back in its response (see CreateStyleCommandHandler).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The concurrency token of a tracked product row, for the client to send back with its next edit (PRD-5). It is PostgreSQL's
    /// row version (<c>xmin</c>), so it changes on every write to the row.
    /// </summary>
    uint VersionOf(Product product);

    /// <summary>Makes the next save fail with a concurrency conflict if the row changed since the client read it (PRD-5).</summary>
    void ExpectVersion(Product product, uint version);

    /// <summary>
    /// Marks the product row as written, so its version moves when only its children changed (images, composition, care, collections).
    /// Without this a second editor's token would still look current.
    /// </summary>
    void Touch(Product product);

    /// <summary>
    /// Serialises slug assignment across concurrent requests for the rest of the transaction (URL-4), so ten products created at once
    /// with the same name each get a distinct, stable slug instead of racing for the same one.
    /// </summary>
    Task AcquireSlugLockAsync(CancellationToken cancellationToken);

    /// <summary>The next value of the sequence every size chart's content version comes from (PCT-5): never repeats, even after a delete.</summary>
    Task<long> NextChartVersionAsync(CancellationToken cancellationToken);

    /// <summary>The concurrency token of a size chart row (CHT-2).</summary>
    uint VersionOf(SizeChart chart);

    void ExpectVersion(SizeChart chart, uint version);

    void Touch(SizeChart chart);
}
