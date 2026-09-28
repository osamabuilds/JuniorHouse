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

    public DbSet<PurchaseOrderFile> PurchaseOrderFiles => Set<PurchaseOrderFile>();

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
            builder.Property(p => p.LatestAcceptableDate).HasColumnName("LATE_ACPT_DT").HasColumnType("date");
            builder.Property(p => p.OverTolerancePercent).HasColumnName("OVER_TOL_PCT").HasColumnType("numeric(5,2)");
            builder.Property(p => p.UnderTolerancePercent).HasColumnName("UNDR_TOL_PCT").HasColumnType("numeric(5,2)");
            builder.Property(p => p.FabricResponsibilityId).HasColumnName("FBRC_RESP_ID");
            builder.HasIndex(p => p.FabricResponsibilityId);
            builder.Property(p => p.StatusId).HasColumnName("PO_STS_ID").IsRequired();
            builder.HasIndex(p => p.StatusId);
            builder.Ignore(p => p.IsDraft);
            builder.HasAuditColumns();

            builder.Metadata.FindNavigation(nameof(PurchaseOrder.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            builder.Metadata.FindNavigation(nameof(PurchaseOrder.StatusHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            builder.Metadata.FindNavigation(nameof(PurchaseOrder.Revisions))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            builder.Metadata.FindNavigation(nameof(PurchaseOrder.VendorCommunications))!.SetPropertyAccessMode(PropertyAccessMode.Field);
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
            builder.Property(h => h.Note).HasColumnName("NOTE").HasMaxLength(500);
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

        // SCRUM-93 task 19 (ADR 0007) - schema-only skeleton; task 20+ adds the revision entity's
        // own creation/transition behaviour.
        modelBuilder.Entity<PurchaseOrderRevision>(builder =>
        {
            builder.ToTable("PO_REV");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            builder.Property(r => r.PoId).HasColumnName("PO_ID").IsRequired();
            builder.Property(r => r.RevisionNumber).HasColumnName("REV_NO").IsRequired();
            builder.HasIndex(r => new { r.PoId, r.RevisionNumber }).IsUnique();
            builder.Property(r => r.InitiatorId).HasColumnName("INIT_ID").IsRequired();
            builder.HasIndex(r => r.InitiatorId);
            builder.Property(r => r.StatusId).HasColumnName("STS_ID").IsRequired();
            builder.HasIndex(r => r.StatusId);
            builder.Property(r => r.ReasonId).HasColumnName("RSN_ID").IsRequired();
            builder.HasIndex(r => r.ReasonId);
            builder.Property(r => r.ImpactNote).HasColumnName("IMPC_NOTE").HasMaxLength(2000).IsRequired();
            builder.Property(r => r.VendorMessage).HasColumnName("VNDR_MSG").HasMaxLength(2000);
            builder.Property(r => r.UnitCost).HasColumnName("UNIT_COST_AMT").HasColumnType("numeric(12,2)").IsRequired();
            builder.Property(r => r.ExpectedDeliveryDate).HasColumnName("EXPC_DLVR_DT").HasColumnType("date").IsRequired();
            builder.Property(r => r.LatestAcceptableDate).HasColumnName("LATE_ACPT_DT").HasColumnType("date");
            builder.Property(r => r.OverTolerancePercent).HasColumnName("OVER_TOL_PCT").HasColumnType("numeric(5,2)");
            builder.Property(r => r.UnderTolerancePercent).HasColumnName("UNDR_TOL_PCT").HasColumnType("numeric(5,2)");
            builder.Property(r => r.PaymentTermId).HasColumnName("PAYM_TERM_ID").IsRequired();
            builder.HasIndex(r => r.PaymentTermId);
            builder.Property(r => r.AdvancePercent).HasColumnName("ADV_PCT").HasColumnType("numeric(5,2)").IsRequired();
            builder.Property(r => r.FabricResponsibilityId).HasColumnName("FBRC_RESP_ID");
            builder.Property(r => r.PoValueBefore).HasColumnName("PO_VAL_BEF_AMT").HasColumnType("numeric(12,2)").IsRequired();
            builder.Property(r => r.PoValueAfter).HasColumnName("PO_VAL_AFT_AMT").HasColumnType("numeric(12,2)").IsRequired();
            builder.Property(r => r.PoValueDiff).HasColumnName("PO_VAL_DIFF_AMT").HasColumnType("numeric(12,2)").IsRequired();
            builder.Property(r => r.AdvanceAmountBefore).HasColumnName("ADV_AMT_BEF").HasColumnType("numeric(12,2)").IsRequired();
            builder.Property(r => r.AdvanceAmountAfter).HasColumnName("ADV_AMT_AFT").HasColumnType("numeric(12,2)").IsRequired();
            builder.Property(r => r.ExpectedDateShiftDays).HasColumnName("EXPC_DT_SHFT_DAY").IsRequired();
            builder.Property(r => r.LatestAcceptableDateShiftDays).HasColumnName("LATE_ACPT_DT_SHFT_DAY");
            builder.Property(r => r.QuantityDiff).HasColumnName("QTY_DIFF").IsRequired();
            builder.Property(r => r.IsBeyondLatestAcceptableDate).HasColumnName("BYND_LATE_IND").IsRequired();
            builder.HasAuditColumns();

            // ADR 0007: at most one Pending (RevisionStatus.Pending = 1) revision per PO at a time,
            // backed at the DB level too (belt-and-braces alongside the command-handler check task
            // 22 adds).
            builder.HasIndex(r => r.PoId).IsUnique().HasFilter("\"STS_ID\" = 1").HasDatabaseName("IX_PO_REV_PO_ID_PEND");

            builder.HasOne<PurchaseOrder>().WithMany(p => p.Revisions)
                .HasForeignKey(r => r.PoId)
                .HasConstraintName("FK_PO_REV_PO_ID")
                .OnDelete(DeleteBehavior.Cascade);

            builder.Metadata.FindNavigation(nameof(PurchaseOrderRevision.StatusHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            builder.Metadata.FindNavigation(nameof(PurchaseOrderRevision.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<PoRevisionStatusHistoryEntry>(builder =>
        {
            builder.ToTable("PO_REV_STS_HIST");
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            builder.Property(h => h.RevisionId).HasColumnName("PO_REV_ID").IsRequired();
            builder.Property(h => h.FromStatusId).HasColumnName("FROM_STS_ID");
            builder.Property(h => h.ToStatusId).HasColumnName("TO_STS_ID").IsRequired();
            builder.Property(h => h.Note).HasColumnName("NOTE").HasMaxLength(2000);
            builder.Property(h => h.InsrDte).HasColumnName("INSR_DTE").IsRequired();
            builder.Property(h => h.InsrBy).HasColumnName("INSR_BY").HasMaxLength(100).IsRequired();
            builder.Ignore(h => h.UpdtDte);
            builder.Ignore(h => h.UpdtBy);
            builder.HasIndex(h => h.RevisionId);

            builder.HasOne<PurchaseOrderRevision>().WithMany(r => r.StatusHistory)
                .HasForeignKey(h => h.RevisionId)
                .HasConstraintName("FK_PO_REV_STS_HIST_PO_REV_ID")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PoRevisionLine>(builder =>
        {
            builder.ToTable("PO_REV_LINE");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            builder.Property(l => l.RevisionId).HasColumnName("PO_REV_ID").IsRequired();
            builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
            builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
            builder.Property(l => l.Qty).HasColumnName("QTY").IsRequired();
            builder.HasIndex(l => new { l.RevisionId, l.SizeId, l.ColourId }).IsUnique();
            builder.HasAuditColumns();

            builder.HasOne<PurchaseOrderRevision>().WithMany(r => r.Lines)
                .HasForeignKey(l => l.RevisionId)
                .HasConstraintName("FK_PO_REV_LINE_PO_REV_ID")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseOrderFile>(builder =>
        {
            builder.ToTable("PO_FILE");
            builder.HasKey(f => f.Id);
            builder.Property(f => f.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            builder.Property(f => f.PoId).HasColumnName("PO_ID").IsRequired();
            builder.HasIndex(f => f.PoId);
            builder.Property(f => f.CategoryId).HasColumnName("CATG_ID").IsRequired();
            builder.HasIndex(f => f.CategoryId);
            builder.Property(f => f.FileName).HasColumnName("FILE_NAME").HasMaxLength(255).IsRequired();
            builder.Property(f => f.StorageKey).HasColumnName("STOR_KEY").HasMaxLength(500).IsRequired();
            builder.Property(f => f.ContentType).HasColumnName("CNTT_TYP").HasMaxLength(100).IsRequired();
            builder.Property(f => f.FileSizeBytes).HasColumnName("FILE_SIZE_BYT").IsRequired();
            builder.Property(f => f.AddedInRevisionId).HasColumnName("ADDD_REV_ID");
            builder.HasIndex(f => f.AddedInRevisionId);
            builder.Property(f => f.RetiredInRevisionId).HasColumnName("RETD_REV_ID");
            builder.HasIndex(f => f.RetiredInRevisionId);
            builder.Property(f => f.IsDeleted).HasColumnName("DELD_IND").IsRequired().HasDefaultValue(false);
            builder.Property(f => f.VendorCommunicationId).HasColumnName("VNDR_COMM_ID");
            builder.HasIndex(f => f.VendorCommunicationId);
            builder.HasAuditColumns();

            builder.HasOne<PurchaseOrder>().WithMany()
                .HasForeignKey(f => f.PoId)
                .HasConstraintName("FK_PO_FILE_PO_ID")
                .OnDelete(DeleteBehavior.Cascade);

            // Not ownership relationships (a file merely references the revision(s) it's effective
            // for, or the communication it's evidence of) - Restrict, not Cascade, since PO_REV/
            // PO_VNDR_COMM rows are permanent history and never deleted in this app.
            builder.HasOne<PurchaseOrderRevision>().WithMany()
                .HasForeignKey(f => f.AddedInRevisionId)
                .HasConstraintName("FK_PO_FILE_ADDD_REV_ID")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PurchaseOrderRevision>().WithMany()
                .HasForeignKey(f => f.RetiredInRevisionId)
                .HasConstraintName("FK_PO_FILE_RETD_REV_ID")
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<PoVendorCommunication>().WithMany()
                .HasForeignKey(f => f.VendorCommunicationId)
                .HasConstraintName("FK_PO_FILE_VNDR_COMM_ID")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PoVendorCommunication>(builder =>
        {
            builder.ToTable("PO_VNDR_COMM");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            builder.Property(c => c.PoId).HasColumnName("PO_ID").IsRequired();
            builder.HasIndex(c => c.PoId);
            builder.Property(c => c.RevisionId).HasColumnName("PO_REV_ID");
            builder.HasIndex(c => c.RevisionId);
            builder.Property(c => c.CommunicationTypeId).HasColumnName("COMM_TYP_ID").IsRequired();
            builder.HasIndex(c => c.CommunicationTypeId);
            builder.Property(c => c.ChannelId).HasColumnName("CHNL_ID").IsRequired();
            builder.HasIndex(c => c.ChannelId);
            builder.Property(c => c.ResponderName).HasColumnName("RSPR_NAME").HasMaxLength(200).IsRequired();
            builder.Property(c => c.ResponseDte).HasColumnName("RSPN_DTE").IsRequired();
            builder.HasAuditColumns();

            builder.HasOne<PurchaseOrder>().WithMany(p => p.VendorCommunications)
                .HasForeignKey(c => c.PoId)
                .HasConstraintName("FK_PO_VNDR_COMM_PO_ID")
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<PurchaseOrderRevision>().WithMany()
                .HasForeignKey(c => c.RevisionId)
                .HasConstraintName("FK_PO_VNDR_COMM_PO_REV_ID")
                .OnDelete(DeleteBehavior.Restrict);
        });

        base.OnModelCreating(modelBuilder);
    }
}
