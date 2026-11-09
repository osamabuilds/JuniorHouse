using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Fulfilment.Application.Abstractions;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Domain.Allocations;
using Romp.Modules.Fulfilment.Domain.PackStation;
using Romp.Modules.Fulfilment.Domain.PickExceptions;

namespace Romp.Modules.Fulfilment.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>FULF</c> schema (fulfilment orders, lines, bin allocations, pick exceptions, pack records, the scan log), including
/// the module's outbox and inbox. Implements <see cref="IFulfilmentDbContext"/> (Application's own port) so handlers never depend on this project.
/// </summary>
public sealed class FulfDbContext(DbContextOptions<FulfDbContext> options) : DbContext(options), IFulfilmentDbContext
{
    public DbSet<FulfilmentOrder> Orders => Set<FulfilmentOrder>();

    public DbSet<FulfilmentLine> Lines => Set<FulfilmentLine>();

    public DbSet<LineAllocation> LineAllocations => Set<LineAllocation>();

    public DbSet<PickExceptionRecord> Exceptions => Set<PickExceptionRecord>();

    public DbSet<PackRecord> PackRecords => Set<PackRecord>();

    public DbSet<ScanEntry> Scans => Set<ScanEntry>();

    async Task IFulfilmentDbContext.SaveChangesAsync(CancellationToken cancellationToken) => await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("FULF");
        modelBuilder.HasOutboxTable("FULF");
        modelBuilder.HasInboxTable("FULF");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FulfDbContext).Assembly);
        FulfilmentLookupConfiguration.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>The system-owned Fulfilment lookups (docs/db/naming.md standard lookup shape), seeded by migration. The codes are what code refers to.</summary>
internal static class FulfilmentLookupConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        Configure<FulfilmentStatusLookup>(modelBuilder, "FULF_STS_LKP",
            (FulfilmentStatuses.Queued, "QUEUED", "Queued", "Allocated and waiting to be picked"),
            (FulfilmentStatuses.Picking, "PICKING", "Picking", "A picker holds the order"),
            (FulfilmentStatuses.Picked, "PICKED", "Picked", "Every unit scanned and the stock decremented; waiting to be packed"),
            (FulfilmentStatuses.Packing, "PACKING", "Packing", "A packer holds the order"),
            (FulfilmentStatuses.Packed, "PACKED", "Packed", "The parcel is sealed and weighed; waiting for the courier"),
            (FulfilmentStatuses.Held, "HELD", "Held", "Every line is waiting for restock"),
            (FulfilmentStatuses.Cancelled, "CANCELLED", "Cancelled", "The order was cancelled or every line was dropped"));

        Configure<LineStatusLookup>(modelBuilder, "LINE_STS_LKP",
            (LineStatuses.Allocated, "ALLOCATED", "Allocated", "Units are set aside in bins"),
            (LineStatuses.AwaitingRestock, "AWAITING_RESTOCK", "Awaiting restock", "Not enough stock on the shelf; the customer has been told"),
            (LineStatuses.Picked, "PICKED", "Picked", "Every unit scanned at the pick station"),
            (LineStatuses.Packed, "PACKED", "Packed", "Every unit scanned at the pack station"),
            (LineStatuses.Cancelled, "CANCELLED", "Cancelled", "Dropped from the order"));

        Configure<ExceptionReasonLookup>(modelBuilder, "EXCP_RSN_LKP",
            (ExceptionReasons.NotFound, "NOT_FOUND", "Not found", "The units are not on the shelf"),
            (ExceptionReasons.Shortfall, "SHORT", "Short", "Fewer units than the system says"),
            (ExceptionReasons.Damaged, "DAMAGED", "Damaged", "The units on the shelf cannot be sent"));

        Configure<ExceptionStatusLookup>(modelBuilder, "EXCP_STS_LKP",
            (ExceptionStatuses.Open, "OPEN", "Open", "Waiting for staff"),
            (ExceptionStatuses.Resolved, "RESOLVED", "Resolved", "Restocked, or the line was dropped"));

        Configure<PackagingKindLookup>(modelBuilder, "PACK_KIND_LKP",
            (PackagingKinds.PolyBag, "POLY_BAG", "Poly bag", "A small soft parcel"),
            (PackagingKinds.Box, "BOX", "Box", "More than one unit, or a heavier parcel"));

        Configure<ScanStageLookup>(modelBuilder, "SCAN_STG_LKP",
            (ScanStages.Pick, "PICK", "Pick", "Scanned at the pick station"),
            (ScanStages.Pack, "PACK", "Pack", "Scanned at the pack station"));

        Configure<ScanOutcomeLookup>(modelBuilder, "SCAN_OTCM_LKP",
            (ScanOutcomes.Accepted, "ACCEPTED", "Accepted", "The scan was counted"),
            (ScanOutcomes.Refused, "REFUSED", "Refused", "The scan was refused and nothing changed"));
    }

    private static void Configure<TLookup>(ModelBuilder modelBuilder, string tableName, params (short Id, string Code, string Name, string Description)[] rows)
        where TLookup : FulfilmentLookup
    {
        EntityTypeBuilder<TLookup> builder = modelBuilder.Entity<TLookup>();
        builder.ToTable(tableName);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(l => l.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(l => l.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(l => l.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(rows.Select(r => (object)new
        {
            Id = r.Id, Code = r.Code, Name = r.Name, Description = (string?)r.Description, SortSeq = r.Id, IsActive = true,
        }).ToArray());
    }
}
