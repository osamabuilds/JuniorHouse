using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Domain.StorefrontSettings;
using Romp.Modules.Storefront.Application.Abstractions;

namespace Romp.Modules.Storefront.Infrastructure.Persistence;

/// <summary>EF Core context for the <c>STFR</c> schema (storefront settings and the published snapshot, ADR 0020).</summary>
public sealed class StfrDbContext(DbContextOptions<StfrDbContext> options) : DbContext(options), IStorefrontDbContext, IStorefrontReadDbContext
{
    public DbSet<PublishedProduct> Products => Set<PublishedProduct>();

    public DbSet<VariantRow> Variants => Set<VariantRow>();

    public DbSet<ImageRow> Images => Set<ImageRow>();

    public DbSet<DerivativeRow> Derivatives => Set<DerivativeRow>();

    public DbSet<CompositionRow> Composition => Set<CompositionRow>();

    public DbSet<CareRow> CareInstructions => Set<CareRow>();

    public DbSet<CollectionRow> Collections => Set<CollectionRow>();

    public DbSet<SizeChartRow> Charts => Set<SizeChartRow>();

    public DbSet<SizeChartLineRow> ChartLines => Set<SizeChartLineRow>();

    public DbSet<StockRow> Stock => Set<StockRow>();

    public DbSet<RebuildRun> RebuildRuns => Set<RebuildRun>();

    public DbSet<ModelMeta> ModelMeta => Set<ModelMeta>();

    public DbSet<NavItem> NavItems => Set<NavItem>();

    public DbSet<AgeStep> AgeSteps => Set<AgeStep>();

    public DbSet<FooterColumn> FooterColumns => Set<FooterColumn>();

    public DbSet<SettingItem> SettingItems => Set<SettingItem>();

    public DbSet<PopularSearch> PopularSearches => Set<PopularSearch>();

    public DbSet<DeliveryRule> DeliveryRules => Set<DeliveryRule>();

    public DbSet<ExchangePolicy> ExchangePolicies => Set<ExchangePolicy>();

    public DbSet<Page> Pages => Set<Page>();

    public DbSet<PageRedirect> PageRedirects => Set<PageRedirect>();

    public DbSet<SettingsDraft> Drafts => Set<SettingsDraft>();

    public DbSet<PublishedSnapshot> Snapshots => Set<PublishedSnapshot>();

    uint IStorefrontDbContext.RowVersionOf(Page page) => Entry(page).Property<uint>("xmin").CurrentValue;

    void IStorefrontDbContext.RequireRowVersion(Page page, uint expectedRowVersion)
    {
        var entry = Entry(page);
        entry.Property<uint>("xmin").OriginalValue = expectedRowVersion;
        entry.State = EntityState.Modified;
    }

    async Task IStorefrontDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    void IStorefrontReadDbContext.ClearTracker() => ChangeTracker.Clear();

    async Task IStorefrontReadDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("STFR");
        modelBuilder.HasOutboxTable("STFR");
        modelBuilder.HasInboxTable("STFR");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StfrDbContext).Assembly);
        StorefrontLookupConfiguration.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
