using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.InboundDeliveries;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.Inspections.Aql;
using Romp.Modules.Quality.Domain.Receiving;
using Romp.Modules.Quality.Domain.SafetyChecks;

namespace Romp.Modules.Quality.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>QC</c> schema (Receiving and Quality Control), including the module's outbox
/// (<c>OUTB_MSG</c>) and its inbox (<c>INBX</c>). Implements <see cref="IQualityDbContext"/> (Application's own port)
/// so handlers never depend on this project (ADR 0002).
/// </summary>
public sealed class QcDbContext(DbContextOptions<QcDbContext> options) : DbContext(options), IQualityDbContext
{
    public DbSet<InboundDelivery> Deliveries => Set<InboundDelivery>();

    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();

    public DbSet<QcBatch> Batches => Set<QcBatch>();

    public DbSet<QcSubBatch> SubBatches => Set<QcSubBatch>();

    public DbSet<Inspection> Inspections => Set<Inspection>();

    public DbSet<SafetyCheck> SafetyChecks => Set<SafetyCheck>();

    public DbSet<ReworkReturn> ReworkReturns => Set<ReworkReturn>();

    public DbSet<AqlSampleRow> AqlSamples => Set<AqlSampleRow>();

    public DbSet<AqlLimitRow> AqlLimits => Set<AqlLimitRow>();

    public DbSet<GrnNumberSequence> GrnSequences => Set<GrnNumberSequence>();

    async Task IQualityDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("QC");
        modelBuilder.HasOutboxTable("QC");
        modelBuilder.HasInboxTable("QC");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QcDbContext).Assembly);
        QualityLookupConfiguration.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
