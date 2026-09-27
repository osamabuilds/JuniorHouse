using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Domain;

namespace Romp.Modules.Vendor.Infrastructure;

/// <summary>
/// EF Core context for the <c>VNDR</c> schema (Vendor &amp; Procurement: vendors and purchase
/// orders), including the module's outbox (<c>OUTB_MSG</c>, SCRUM-165) - the only Sprint 1 module
/// that raises domain events. Implements <see cref="IVendorDbContext"/> (Application's own
/// abstraction) so Application-layer command and query handlers can use it without depending on
/// this Infrastructure project (ADR 0002).
/// </summary>
public sealed class VendorDbContext(DbContextOptions<VendorDbContext> options)
    : DbContext(options), IVendorDbContext
{
    public DbSet<Domain.Vendor> Vendors => Set<Domain.Vendor>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    async Task IVendorDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("VNDR");
        modelBuilder.HasOutboxTable("VNDR");

        modelBuilder.Entity<Domain.Vendor>(builder =>
        {
            builder.ToTable("VNDR", "VNDR");
            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            builder.Property(v => v.Name).HasColumnName("VNDR_NAME").HasMaxLength(200).IsRequired();
            builder.Property(v => v.ContactName).HasColumnName("CNTC_NAME").HasMaxLength(100).IsRequired();
            builder.Property(v => v.ContactPhone).HasColumnName("CNTC_PHON").HasMaxLength(20).IsRequired();
            builder.Property(v => v.ContactEmail).HasColumnName("CNTC_EML").HasMaxLength(200);
            builder.Property(v => v.CityId).HasColumnName("CITY_ID").IsRequired();
            builder.HasIndex(v => v.CityId);
            builder.Property(v => v.PaymentTermId).HasColumnName("PAYM_TERM_ID").IsRequired();
            builder.HasIndex(v => v.PaymentTermId);
            builder.Property(v => v.OnTimePercent).HasColumnName("ONTM_PCT").HasColumnType("numeric(5,2)");
            builder.Property(v => v.OnQuantityPercent).HasColumnName("ONQT_PCT").HasColumnType("numeric(5,2)");
            builder.Property(v => v.DefectRatePercent).HasColumnName("DFCT_RATE_PCT").HasColumnType("numeric(5,2)");
            builder.Property(v => v.IsActive).HasColumnName("ACT_IND").IsRequired();
            builder.HasAuditColumns();

            builder.Metadata.FindNavigation(nameof(Domain.Vendor.Specialisations))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<VendorSpecialisation>(builder =>
        {
            builder.ToTable("VNDR_SPCL_MAP");
            builder.HasKey(s => new { s.VendorId, s.SpecialisationId });
            builder.Property(s => s.VendorId).HasColumnName("VNDR_ID");
            builder.Property(s => s.SpecialisationId).HasColumnName("SPCL_ID");
            builder.HasIndex(s => s.SpecialisationId);

            builder.HasOne<Domain.Vendor>().WithMany(v => v.Specialisations)
                .HasForeignKey(s => s.VendorId)
                .HasConstraintName("FK_VNDR_SPCL_MAP_VNDR_ID")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseOrder>(builder =>
        {
            builder.ToTable("PO_MAIN");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            builder.Property(p => p.PoNo).HasColumnName("PO_NO").HasMaxLength(15).IsRequired();
            builder.HasIndex(p => p.PoNo).IsUnique();
            builder.Property(p => p.VendorId).HasColumnName("VNDR_ID").IsRequired();
            builder.HasIndex(p => p.VendorId);
            builder.Property(p => p.StyleId).HasColumnName("STYL_ID").IsRequired();
            builder.HasIndex(p => p.StyleId);
            builder.Property(p => p.UnitCost).HasColumnName("UNIT_COST_AMT").HasColumnType("numeric(12,2)");
            builder.Property(p => p.ExpectedDeliveryDate).HasColumnName("EXPC_DLVR_DT").HasColumnType("date");
            builder.Property(p => p.PaymentTermId).HasColumnName("PAYM_TERM_ID").IsRequired();
            builder.HasIndex(p => p.PaymentTermId);
            builder.Property(p => p.AdvancePercent).HasColumnName("ADV_PCT").HasColumnType("numeric(5,2)");
            builder.Property(p => p.StatusId).HasColumnName("PO_STS_ID").IsRequired();
            builder.HasIndex(p => p.StatusId);
            builder.Ignore(p => p.IsDraft);
            builder.HasAuditColumns();

            builder.Metadata.FindNavigation(nameof(PurchaseOrder.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            builder.Metadata.FindNavigation(nameof(PurchaseOrder.StatusHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PoLine>(builder =>
        {
            builder.ToTable("PO_LINE");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            builder.Property(l => l.PoId).HasColumnName("PO_ID").IsRequired();
            builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
            builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
            builder.Property(l => l.Qty).HasColumnName("QTY").IsRequired();
            builder.HasIndex(l => new { l.PoId, l.SizeId, l.ColourId }).IsUnique();
            builder.HasAuditColumns();

            builder.HasOne<PurchaseOrder>().WithMany(p => p.Lines)
                .HasForeignKey(l => l.PoId)
                .HasConstraintName("FK_PO_LINE_PO_ID")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PoStatusHistoryEntry>(builder =>
        {
            builder.ToTable("PO_STS_HIST");
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            builder.Property(h => h.PoId).HasColumnName("PO_ID").IsRequired();
            builder.Property(h => h.PoStatusId).HasColumnName("PO_STS_ID").IsRequired();
            builder.Property(h => h.CancelReasonId).HasColumnName("PO_CNCL_RSN_ID");
            builder.Property(h => h.InsrDte).HasColumnName("INSR_DTE").IsRequired();
            builder.Property(h => h.InsrBy).HasColumnName("INSR_BY").HasMaxLength(100).IsRequired();
            builder.Ignore(h => h.UpdtDte);
            builder.Ignore(h => h.UpdtBy);
            builder.HasIndex(h => h.PoId);

            builder.HasOne<PurchaseOrder>().WithMany(p => p.StatusHistory)
                .HasForeignKey(h => h.PoId)
                .HasConstraintName("FK_PO_STS_HIST_PO_ID")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PoNumberSequence>(builder =>
        {
            builder.ToTable("PO_NO_SEQ");
            builder.HasKey(s => s.Year);
            builder.Property(s => s.Year).HasColumnName("YR");
            builder.Property(s => s.Seq).HasColumnName("SEQ").IsRequired();
        });

        base.OnModelCreating(modelBuilder);
    }
}
