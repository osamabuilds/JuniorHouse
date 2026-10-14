using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Production.Domain;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Domain.ProductionRuns;

namespace Romp.Modules.Production.Infrastructure.Deliveries;

internal sealed class DeliveryNoteConfiguration : IEntityTypeConfiguration<DeliveryNote>
{
    public void Configure(EntityTypeBuilder<DeliveryNote> builder)
    {
        builder.ToTable("DLVR_NOTE");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(n => n.RunId).HasColumnName("RUN_ID").IsRequired();
        builder.Property(n => n.VendorDnNo).HasColumnName("VNDR_DN_NO").HasMaxLength(50).IsRequired();
        builder.HasIndex(n => new { n.RunId, n.VendorDnNo }).IsUnique(); // AC-20
        builder.Property(n => n.DispatchDate).HasColumnName("DSPT_DT").HasColumnType("date");
        builder.Property(n => n.IsFinalShipment).HasColumnName("FNL_SHIP_IND").IsRequired();
        builder.Property(n => n.RevisionNumber).HasColumnName("REV_NO").IsRequired();
        builder.Property(n => n.TimingId).HasColumnName("DLVR_TMNG_ID").IsRequired();
        builder.HasIndex(n => n.TimingId);
        builder.Property(n => n.DaysLate).HasColumnName("LATE_DAY_CNT").IsRequired();
        builder.Property(n => n.QuantityStatusId).HasColumnName("DLVR_QTY_STS_ID").IsRequired();
        builder.HasIndex(n => n.QuantityStatusId);
        builder.Property(n => n.OverQty).HasColumnName("OVER_QTY").IsRequired();
        builder.Property(n => n.ShortQty).HasColumnName("SHRT_QTY").IsRequired();
        builder.Property(n => n.FabricDispatchDate).HasColumnName("FBRC_DSPT_DT").HasColumnType("date");
        builder.Property(n => n.IsRompFabricDelay).HasColumnName("ROMP_FBRC_DLAY_IND").IsRequired();
        builder.Property(n => n.ActingPerson).HasColumnName("ACTR_NAME").HasMaxLength(100).IsRequired();
        builder.HasAuditColumns();

        builder.HasOne<ProductionRun>().WithMany(r => r.DeliveryNotes).HasForeignKey(n => n.RunId)
            .HasConstraintName("FK_DLVR_NOTE_RUN_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DeliveryTimingLookup>().WithMany().HasForeignKey(n => n.TimingId)
            .HasConstraintName("FK_DLVR_NOTE_DLVR_TMNG_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeliveryQuantityStatusLookup>().WithMany().HasForeignKey(n => n.QuantityStatusId)
            .HasConstraintName("FK_DLVR_NOTE_DLVR_QTY_STS_ID").OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(DeliveryNote.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class DeliveryNoteLineConfiguration : IEntityTypeConfiguration<DeliveryNoteLine>
{
    public void Configure(EntityTypeBuilder<DeliveryNoteLine> builder)
    {
        builder.ToTable("DLVR_NOTE_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.NoteId).HasColumnName("NOTE_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(l => l.Qty).HasColumnName("QTY").IsRequired();
        builder.Property(l => l.OverQty).HasColumnName("OVER_QTY").IsRequired();
        builder.Property(l => l.ShortQty).HasColumnName("SHRT_QTY").IsRequired();
        builder.HasIndex(l => new { l.NoteId, l.SizeId, l.ColourId }).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<DeliveryNote>().WithMany(n => n.Lines).HasForeignKey(l => l.NoteId)
            .HasConstraintName("FK_DLVR_NOTE_LINE_NOTE_ID").OnDelete(DeleteBehavior.Cascade);
    }
}
