using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Domain.CompositionCare;
using Romp.Modules.Catalog.Domain.ProductMedia;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Domain.SizeCharts;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Domain.Urls;

namespace Romp.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>CTLG</c> schema (style master and, from Sprint 7, products, images, composition and care, size charts and
/// URL history), including the module's outbox and inbox. Implements <see cref="ICatalogDbContext"/> (Application's own abstraction) so
/// Application-layer command and query handlers can use it without depending on this Infrastructure project (ADR 0002).
/// </summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options), ICatalogDbContext
{
    /// <summary>A fixed key for the transaction-scoped advisory lock that serialises slug assignment (URL-4).</summary>
    private const long SlugLockKey = 7_201_001;

    public DbSet<Style> Styles => Set<Style>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<PlannedVariant> Variants => Set<PlannedVariant>();

    public DbSet<LabelledVariant> LabelledVariants => Set<LabelledVariant>();

    public DbSet<PriceHistoryEntry> PriceHistory => Set<PriceHistoryEntry>();

    public DbSet<PathHistoryEntry> PathHistory => Set<PathHistoryEntry>();

    public DbSet<ProductImage> Images => Set<ProductImage>();

    public DbSet<CompositionLine> CompositionLines => Set<CompositionLine>();

    public DbSet<ProductCare> CareInstructions => Set<ProductCare>();

    public DbSet<SizeChart> Charts => Set<SizeChart>();

    public DbSet<SizeChartLine> ChartLines => Set<SizeChartLine>();

    async Task ICatalogDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    public uint VersionOf(Product product) => Entry(product).Property<uint>("xmin").CurrentValue;

    public void ExpectVersion(Product product, uint version) => Entry(product).Property<uint>("xmin").OriginalValue = version;

    public void Touch(Product product) => Entry(product).Property(p => p.Name).IsModified = true;

    public uint VersionOf(SizeChart chart) => Entry(chart).Property<uint>("xmin").CurrentValue;

    public void ExpectVersion(SizeChart chart, uint version) => Entry(chart).Property<uint>("xmin").OriginalValue = version;

    public void Touch(SizeChart chart) => Entry(chart).Property(c => c.Name).IsModified = true;

    public Task<long> NextChartVersionAsync(CancellationToken cancellationToken) =>
        Database.SqlQuery<long>($"SELECT nextval('\"CTLG\".\"CHRT_VER_SEQ\"') AS \"Value\"").SingleAsync(cancellationToken);

    public Task AcquireSlugLockAsync(CancellationToken cancellationToken) =>
        Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({SlugLockKey})", cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("CTLG");
        modelBuilder.HasSequence<long>("CHRT_VER_SEQ", "CTLG");
        modelBuilder.HasOutboxTable("CTLG");
        modelBuilder.HasInboxTable("CTLG");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
