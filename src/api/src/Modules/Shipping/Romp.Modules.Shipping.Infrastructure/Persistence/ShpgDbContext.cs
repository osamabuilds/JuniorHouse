using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Domain.Bookings;
using Romp.Modules.Shipping.Domain.Manifests;

namespace Romp.Modules.Shipping.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>SHPG</c> schema (shipments, their status history, manifests), including the module's outbox and inbox. Implements
/// <see cref="IShippingDbContext"/> (Application's own port) so handlers never depend on this project.
/// </summary>
public sealed class ShpgDbContext(DbContextOptions<ShpgDbContext> options) : DbContext(options), IShippingDbContext
{
    public DbSet<Shipment> Shipments => Set<Shipment>();

    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();

    public DbSet<Manifest> Manifests => Set<Manifest>();

    public DbSet<ManifestLine> ManifestLines => Set<ManifestLine>();

    async Task IShippingDbContext.SaveChangesAsync(CancellationToken cancellationToken) => await SaveChangesAsync(cancellationToken);

    public async Task<bool> ClaimForBookingAsync(long shipmentId, DateTimeOffset now, DateTimeOffset until, CancellationToken cancellationToken)
    {
        // One conditional statement (every identifier quoted): it succeeds only while the shipment is pending and due, so two workers get one yes.
        var rows = await Database.ExecuteSqlRawAsync(
            "UPDATE \"SHPG\".\"SHPG_MAIN\" SET \"NXT_ATMP_DTE\" = {0} WHERE \"ID\" = {1} AND \"SHPG_STS_ID\" = 1 AND \"NXT_ATMP_DTE\" IS NOT NULL AND \"NXT_ATMP_DTE\" <= {2}",
            [until, shipmentId, now],
            cancellationToken);
        return rows == 1;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("SHPG");
        modelBuilder.HasOutboxTable("SHPG");
        modelBuilder.HasInboxTable("SHPG");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShpgDbContext).Assembly);
        ShippingLookupConfiguration.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("SHPG_MAIN", table =>
        {
            table.HasCheckConstraint("CK_SHPG_MAIN_CASH_AMT", "\"CASH_AMT\" >= 0");
            table.HasCheckConstraint("CK_SHPG_MAIN_WGHT_GRM", "\"WGHT_GRM\" > 0");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.OrderId).HasColumnName("ORDR_ID").IsRequired();
        builder.Property(s => s.OrderNumber).HasColumnName("ORDR_NO").HasMaxLength(30).IsRequired();
        builder.Property(s => s.StatusId).HasColumnName("SHPG_STS_ID").IsRequired();
        builder.Property(s => s.StatusAt).HasColumnName("STS_DTE").IsRequired();
        builder.Property(s => s.CourierId).HasColumnName("CRER_ID");
        builder.Property(s => s.Waybill).HasColumnName("WYBL_NO").HasMaxLength(Shipment.MaxWaybillLength);
        builder.Property(s => s.BookingSourceId).HasColumnName("BKNG_SRC_ID");
        builder.Property(s => s.BookedAt).HasColumnName("BKNG_DTE");
        builder.Property(s => s.CashToCollect).HasColumnName("CASH_AMT").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(s => s.WeightGrams).HasColumnName("WGHT_GRM").IsRequired();
        builder.Property(s => s.Attempts).HasColumnName("ATMP_CNT").IsRequired();
        builder.Property(s => s.LastError).HasColumnName("LAST_ERR").HasMaxLength(20);
        builder.Property(s => s.LastNote).HasColumnName("LAST_NOTE").HasMaxLength(Shipment.MaxNoteLength);
        builder.Property(s => s.NextAttemptAt).HasColumnName("NXT_ATMP_DTE");
        builder.Property(s => s.NeedsStaff).HasColumnName("NEED_STAF_IND").IsRequired();
        builder.Property(s => s.FailedAttempts).HasColumnName("FAIL_ATMP_CNT").IsRequired();
        builder.Property(s => s.DeliveredAt).HasColumnName("DLVR_DTE");
        builder.Property(s => s.ManifestId).HasColumnName("MNFS_ID");
        builder.Property(s => s.LabelPrintCount).HasColumnName("LBL_PRT_CNT").IsRequired();
        builder.Property(s => s.PolledAt).HasColumnName("POLL_DTE");
        builder.Ignore(s => s.DomainEvents);
        builder.Ignore(s => s.IsBooked);

        // One shipment per order (BKG-5); one waybill per parcel, whoever booked it (MBQ-5).
        builder.HasIndex(s => s.OrderId).IsUnique().HasDatabaseName("UQ_SHPG_MAIN_ORDR_ID");
        builder.HasIndex(s => s.Waybill).IsUnique().HasFilter("\"WYBL_NO\" IS NOT NULL").HasDatabaseName("UQ_SHPG_MAIN_WYBL_NO");
        // The booking worker and the manual queue read pending shipments by due time; lists read one status.
        builder.HasIndex(s => new { s.StatusId, s.NextAttemptAt }).HasDatabaseName("IX_SHPG_MAIN_SHPG_STS_ID_NXT_ATMP_DTE");
        builder.HasIndex(s => new { s.CourierId, s.StatusId }).HasDatabaseName("IX_SHPG_MAIN_CRER_ID_SHPG_STS_ID");
        builder.HasIndex(s => s.ManifestId).HasDatabaseName("IX_SHPG_MAIN_MNFS_ID");
        builder.HasIndex(s => s.BookingSourceId).HasDatabaseName("IX_SHPG_MAIN_BKNG_SRC_ID");
        builder.HasAuditColumns();

        builder.HasOne<ShipmentStatusLookup>().WithMany().HasForeignKey(s => s.StatusId).HasConstraintName("FK_SHPG_MAIN_SHPG_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourierLookup>().WithMany().HasForeignKey(s => s.CourierId).HasConstraintName("FK_SHPG_MAIN_CRER_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BookingSourceLookup>().WithMany().HasForeignKey(s => s.BookingSourceId).HasConstraintName("FK_SHPG_MAIN_BKNG_SRC_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Manifest>().WithMany().HasForeignKey(s => s.ManifestId).HasConstraintName("FK_SHPG_MAIN_MNFS_MAIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(s => s.Events).WithOne().HasForeignKey(e => e.ShipmentId).HasConstraintName("FK_SHPG_EVNT_SHPG_MAIN").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ShipmentEventConfiguration : IEntityTypeConfiguration<ShipmentEvent>
{
    public void Configure(EntityTypeBuilder<ShipmentEvent> builder)
    {
        builder.ToTable("SHPG_EVNT");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(e => e.ShipmentId).HasColumnName("SHPG_ID").IsRequired();
        builder.Property(e => e.StatusId).HasColumnName("SHPG_STS_ID").IsRequired();
        builder.Property(e => e.CourierEventId).HasColumnName("CRER_EVNT_ID").HasMaxLength(80).IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("EVNT_DTE").IsRequired();
        builder.Property(e => e.SourceId).HasColumnName("EVNT_SRC_ID").IsRequired();

        // The same courier event twice is the same event (TRK-3): the database decides a race.
        builder.HasIndex(e => new { e.ShipmentId, e.CourierEventId }).IsUnique().HasDatabaseName("UQ_SHPG_EVNT_SHPG_ID_CRER_EVNT_ID");
        builder.HasIndex(e => e.StatusId).HasDatabaseName("IX_SHPG_EVNT_SHPG_STS_ID");
        builder.HasIndex(e => e.SourceId).HasDatabaseName("IX_SHPG_EVNT_EVNT_SRC_ID");
        builder.HasAuditColumns();

        builder.HasOne<ShipmentStatusLookup>().WithMany().HasForeignKey(e => e.StatusId).HasConstraintName("FK_SHPG_EVNT_SHPG_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EventSourceLookup>().WithMany().HasForeignKey(e => e.SourceId).HasConstraintName("FK_SHPG_EVNT_EVNT_SRC_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ManifestConfiguration : IEntityTypeConfiguration<Manifest>
{
    public void Configure(EntityTypeBuilder<Manifest> builder)
    {
        builder.ToTable("MNFS_MAIN");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(m => m.CourierId).HasColumnName("CRER_ID").IsRequired();
        builder.Property(m => m.BusinessDate).HasColumnName("MNFS_DTE").HasColumnType("date").IsRequired();
        builder.Property(m => m.StatusId).HasColumnName("MNFS_STS_ID").IsRequired();
        builder.Property(m => m.CountedParcels).HasColumnName("CNTD_CNT");
        builder.Property(m => m.CountedBy).HasColumnName("CNTD_BY").HasMaxLength(100);
        builder.Property(m => m.CountedAt).HasColumnName("CNTD_DTE");
        builder.Property(m => m.Note).HasColumnName("NOTE").HasMaxLength(Manifest.MaxNoteLength);
        builder.Ignore(m => m.DomainEvents);
        builder.Ignore(m => m.IsFrozen);

        // One manifest per courier per business day (MNF-4).
        builder.HasIndex(m => new { m.CourierId, m.BusinessDate }).IsUnique().HasDatabaseName("UQ_MNFS_MAIN_CRER_ID_MNFS_DTE");
        builder.HasIndex(m => m.StatusId).HasDatabaseName("IX_MNFS_MAIN_MNFS_STS_ID");
        builder.HasAuditColumns();

        builder.HasOne<CourierLookup>().WithMany().HasForeignKey(m => m.CourierId).HasConstraintName("FK_MNFS_MAIN_CRER_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ManifestStatusLookup>().WithMany().HasForeignKey(m => m.StatusId).HasConstraintName("FK_MNFS_MAIN_MNFS_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(m => m.Lines).WithOne().HasForeignKey(l => l.ManifestId).HasConstraintName("FK_MNFS_LINE_MNFS_MAIN").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(m => m.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ManifestLineConfiguration : IEntityTypeConfiguration<ManifestLine>
{
    public void Configure(EntityTypeBuilder<ManifestLine> builder)
    {
        builder.ToTable("MNFS_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.ManifestId).HasColumnName("MNFS_ID").IsRequired();
        builder.Property(l => l.ShipmentId).HasColumnName("SHPG_ID").IsRequired();
        builder.Property(l => l.HandedOver).HasColumnName("HAND_IND").IsRequired();

        builder.HasIndex(l => new { l.ManifestId, l.ShipmentId }).IsUnique().HasDatabaseName("UQ_MNFS_LINE_MNFS_ID_SHPG_ID");
        builder.HasIndex(l => l.ShipmentId).HasDatabaseName("IX_MNFS_LINE_SHPG_ID");
        builder.HasAuditColumns();

        builder.HasOne<Shipment>().WithMany().HasForeignKey(l => l.ShipmentId).HasConstraintName("FK_MNFS_LINE_SHPG_MAIN").OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>The system-owned Shipping lookups (docs/db/naming.md standard lookup shape), seeded by migration.</summary>
internal static class ShippingLookupConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        Configure<ShipmentStatusLookup>(modelBuilder, "SHPG_STS_LKP",
            (ShipmentStatuses.PendingBooking, "PENDING_BOOKING", "Pending booking", "Waiting for a courier to take it"),
            (ShipmentStatuses.Booked, "BOOKED", "Booked", "A courier gave a waybill"),
            (ShipmentStatuses.PickedUp, "PICKED_UP", "Picked up", "The courier collected it"),
            (ShipmentStatuses.InTransit, "IN_TRANSIT", "In transit", "On its way"),
            (ShipmentStatuses.OutForDelivery, "OUT_FOR_DELIVERY", "Out for delivery", "With the rider"),
            (ShipmentStatuses.FailedAttempt, "FAILED_ATTEMPT", "Failed attempt", "The delivery attempt failed"),
            (ShipmentStatuses.Delivered, "DELIVERED", "Delivered", "Handed to the customer"),
            (ShipmentStatuses.ReturnedToOrigin, "RETURNED_TO_ORIGIN", "Returned to origin", "Back with us unopened"),
            (ShipmentStatuses.Cancelled, "CANCELLED", "Cancelled", "The order was cancelled before pick-up"));

        Configure<CourierLookup>(modelBuilder, "CRER_LKP",
            (Couriers.Primary, "PRIMARY", "Primary courier", "The default courier"),
            (Couriers.Secondary, "SECONDARY", "Secondary courier", "Takes the parcel when the primary cannot be reached"));

        Configure<BookingSourceLookup>(modelBuilder, "BKNG_SRC_LKP",
            (BookingSources.Courier, "COURIER", "Courier", "The waybill came from the courier's booking"),
            (BookingSources.Manual, "MANUAL", "Manual", "Staff entered the waybill"));

        Configure<EventSourceLookup>(modelBuilder, "EVNT_SRC_LKP",
            (EventSources.Push, "PUSH", "Push", "The courier sent it"),
            (EventSources.Poll, "POLL", "Poll", "We asked the courier"),
            (EventSources.System, "SYSTEM", "System", "Raised by Romp"));

        Configure<ManifestStatusLookup>(modelBuilder, "MNFS_STS_LKP",
            (ManifestStatuses.Open, "OPEN", "Open", "Taking parcels; not counted yet"),
            (ManifestStatuses.Matched, "MATCHED", "Matched", "The courier took exactly the listed parcels"),
            (ManifestStatuses.Mismatch, "MISMATCH", "Mismatch", "The count differs; staff resolve it"),
            (ManifestStatuses.Closed, "CLOSED", "Closed", "A mismatch that was resolved"));
    }

    private static void Configure<TLookup>(ModelBuilder modelBuilder, string tableName, params (short Id, string Code, string Name, string Description)[] rows)
        where TLookup : ShippingLookup
    {
        var builder = modelBuilder.Entity<TLookup>();
        builder.ToTable(tableName);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(l => l.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(l => l.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(l => l.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(rows.Select(r => (object)new { Id = r.Id, Code = r.Code, Name = r.Name, Description = (string?)r.Description, SortSeq = r.Id, IsActive = true }).ToArray());
    }
}
