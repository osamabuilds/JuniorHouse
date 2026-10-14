using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Domain.Samples;

namespace Romp.Modules.Production.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>PROD</c> schema (Production Tracking), including the module's outbox
/// (<c>OUTB_MSG</c>) and its inbox (<c>INBX</c>, the first real consumer on the shared dispatcher).
/// Implements <see cref="IProductionDbContext"/> (Application's own port) so handlers never depend on this project (ADR 0002).
/// </summary>
public sealed class ProductionDbContext(DbContextOptions<ProductionDbContext> options)
    : DbContext(options), IProductionDbContext
{
    public DbSet<ProductionRun> Runs => Set<ProductionRun>();

    public DbSet<Milestone> Milestones => Set<Milestone>();

    public DbSet<PpSampleRound> PpRounds => Set<PpSampleRound>();

    public DbSet<PpSampleFile> PpFiles => Set<PpSampleFile>();

    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();

    public DbSet<DeliveryNoteLine> DeliveryNoteLines => Set<DeliveryNoteLine>();

    async Task IProductionDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("PROD");
        modelBuilder.HasOutboxTable("PROD");
        modelBuilder.HasInboxTable("PROD");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductionDbContext).Assembly);
        ProductionLookupConfiguration.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
