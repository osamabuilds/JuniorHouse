using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.LabelWork;
using Romp.Modules.Labelling.Domain.PrintJobs;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>SKU</c> schema (SKU and barcode generation, tag printing), including the module's outbox and inbox.
/// Implements <see cref="ILabellingDbContext"/> (Application's own port) so handlers never depend on this project (ADR 0002).
/// </summary>
public sealed class SkuDbContext(DbContextOptions<SkuDbContext> options) : DbContext(options), ILabellingDbContext
{
    public DbSet<Variant> Variants => Set<Variant>();

    public DbSet<VariantGtin> Gtins => Set<VariantGtin>();

    public DbSet<LabelWorkItem> WorkItems => Set<LabelWorkItem>();

    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();

    public DbSet<PrintAttempt> PrintAttempts => Set<PrintAttempt>();

    public DbSet<PrinterLookup> Printers => Set<PrinterLookup>();

    public DbSet<GtinNumberSequence> GtinSequences => Set<GtinNumberSequence>();

    public DbSet<JobNumberSequence> JobSequences => Set<JobNumberSequence>();

    async Task ILabellingDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("SKU");
        modelBuilder.HasOutboxTable("SKU");
        modelBuilder.HasInboxTable("SKU");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SkuDbContext).Assembly);
        LabellingLookupConfiguration.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
