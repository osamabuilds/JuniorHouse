using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Warehouse.Domain.Reservations;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Infrastructure.Reservations;

internal sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("STCK_RSRV");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.PlacementKey).HasColumnName("PLCE_KEY").HasMaxLength(StockReservation.MaxKeyLength).IsRequired();
        builder.Property(r => r.StatusId).HasColumnName("RSRV_STS_ID").IsRequired();
        builder.Property(r => r.HeldUntil).HasColumnName("HELD_UNTL_DTE");
        builder.Property(r => r.OrderNumber).HasColumnName("ORDR_NO").HasMaxLength(30);
        builder.Ignore(r => r.DomainEvents);

        // One reservation per placement key: a repeated reserve is the same reservation (RSV-4).
        builder.HasIndex(r => r.PlacementKey).IsUnique().HasDatabaseName("UQ_STCK_RSRV_PLCE_KEY");
        // The sweeper looks for holds past their expiry.
        builder.HasIndex(r => new { r.StatusId, r.HeldUntil }).HasDatabaseName("IX_STCK_RSRV_RSRV_STS_ID_HELD_UNTL_DTE");
        builder.HasAuditColumns();

        builder.HasOne<ReservationStatusLookup>().WithMany().HasForeignKey(r => r.StatusId).HasConstraintName("FK_STCK_RSRV_RSRV_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.Metadata.FindNavigation(nameof(StockReservation.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ReservationLineConfiguration : IEntityTypeConfiguration<ReservationLine>
{
    public void Configure(EntityTypeBuilder<ReservationLine> builder)
    {
        builder.ToTable("STCK_RSRV_LINE", table => table.HasCheckConstraint("CK_STCK_RSRV_LINE_QTY", "\"QTY\" > 0"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.ReservationId).HasColumnName("RSRV_ID").IsRequired();
        builder.Property(l => l.VariantRefId).HasColumnName("VRNT_REF_ID").IsRequired();
        builder.Property(l => l.Quantity).HasColumnName("QTY").IsRequired();

        builder.HasIndex(l => new { l.ReservationId, l.VariantRefId }).IsUnique().HasDatabaseName("UQ_STCK_RSRV_LINE_RSRV_ID_VRNT_REF_ID");
        builder.HasIndex(l => l.VariantRefId).HasDatabaseName("IX_STCK_RSRV_LINE_VRNT_REF_ID");
        builder.HasAuditColumns();

        builder.HasOne<StockReservation>().WithMany(r => r.Lines).HasForeignKey(l => l.ReservationId).HasConstraintName("FK_STCK_RSRV_LINE_STCK_RSRV").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<VariantRef>().WithMany().HasForeignKey(l => l.VariantRefId).HasConstraintName("FK_STCK_RSRV_LINE_VRNT_REF").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReservedBalanceConfiguration : IEntityTypeConfiguration<ReservedBalance>
{
    public void Configure(EntityTypeBuilder<ReservedBalance> builder)
    {
        builder.ToTable("STCK_RSRV_BAL", table => table.HasCheckConstraint("CK_STCK_RSRV_BAL_RSRV_QTY", "\"RSRV_QTY\" >= 0"));
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("VRNT_REF_ID").ValueGeneratedNever();
        builder.Property(b => b.Quantity).HasColumnName("RSRV_QTY").IsRequired();
        builder.Ignore(b => b.VariantRefId);
        builder.HasAuditColumns();

        builder.HasOne<VariantRef>().WithOne().HasForeignKey<ReservedBalance>(b => b.Id).HasConstraintName("FK_STCK_RSRV_BAL_VRNT_REF").OnDelete(DeleteBehavior.Restrict);
    }
}
