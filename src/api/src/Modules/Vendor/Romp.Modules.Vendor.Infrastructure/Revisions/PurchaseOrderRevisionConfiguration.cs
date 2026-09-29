using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Revisions;

namespace Romp.Modules.Vendor.Infrastructure.Revisions;

/// <summary>
/// SCRUM-93 task 19 (ADR 0007) - schema-only skeleton; task 20+ adds the revision entity's
/// own creation/transition behaviour.
/// </summary>
internal sealed class PurchaseOrderRevisionConfiguration : IEntityTypeConfiguration<PurchaseOrderRevision>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderRevision> builder)
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
    }
}
