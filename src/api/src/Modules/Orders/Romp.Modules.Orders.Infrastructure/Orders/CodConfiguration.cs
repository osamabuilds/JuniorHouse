using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Infrastructure.Orders;

internal sealed class CodAttemptConfiguration : IEntityTypeConfiguration<CodAttempt>
{
    public void Configure(EntityTypeBuilder<CodAttempt> builder)
    {
        builder.ToTable("COD_ATMP");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.OrderId).HasColumnName("ORDR_ID").IsRequired();
        builder.Property(a => a.Number).HasColumnName("ATMP_NO").IsRequired();
        builder.Property(a => a.OutcomeId).HasColumnName("COD_OTCM_ID").IsRequired();
        builder.Property(a => a.ActorKindId).HasColumnName("ACTR_KIND_ID").IsRequired();
        builder.Property(a => a.ActorRef).HasColumnName("ACTR_REF").HasMaxLength(64);
        builder.Property(a => a.Note).HasColumnName("NOTE").HasMaxLength(OrderHistory.MaxNoteLength);
        builder.Property(a => a.At).HasColumnName("OCCR_DTE").IsRequired();

        builder.HasIndex(a => new { a.OrderId, a.Number }).IsUnique().HasDatabaseName("UQ_COD_ATMP_ORDR_ID_ATMP_NO");
        builder.HasIndex(a => a.OutcomeId).HasDatabaseName("IX_COD_ATMP_COD_OTCM_ID");
        builder.HasIndex(a => a.ActorKindId).HasDatabaseName("IX_COD_ATMP_ACTR_KIND_ID");
        builder.HasAuditColumns();

        builder.HasOne<Order>().WithMany(o => o.CodAttempts).HasForeignKey(a => a.OrderId).HasConstraintName("FK_COD_ATMP_ORDR_MAIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CodOutcomeLookup>().WithMany().HasForeignKey(a => a.OutcomeId).HasConstraintName("FK_COD_ATMP_COD_OTCM").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ActorKindLookup>().WithMany().HasForeignKey(a => a.ActorKindId).HasConstraintName("FK_COD_ATMP_ACTR_KIND").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CodRiskConfiguration : IEntityTypeConfiguration<CodRisk>
{
    public void Configure(EntityTypeBuilder<CodRisk> builder)
    {
        builder.ToTable("COD_RISK", table => table.HasCheckConstraint("CK_COD_RISK_CNTS", "\"RFSL_CNT\" >= 0 AND \"NOAN_CNCL_CNT\" >= 0"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.Mobile).HasColumnName("CNTC_PHON").HasMaxLength(20).IsRequired();
        builder.Property(r => r.RefusalCount).HasColumnName("RFSL_CNT").IsRequired();
        builder.Property(r => r.NoAnswerCancelCount).HasColumnName("NOAN_CNCL_CNT").IsRequired();
        builder.Property(r => r.IsFlagged).HasColumnName("FLG_IND").IsRequired();
        builder.Property(r => r.ClearedBy).HasColumnName("CLRD_BY").HasMaxLength(64);
        builder.Property(r => r.ClearedAt).HasColumnName("CLRD_DTE");

        builder.HasIndex(r => r.Mobile).IsUnique().HasDatabaseName("UQ_COD_RISK_CNTC_PHON");
        builder.HasIndex(r => r.IsFlagged).HasDatabaseName("IX_COD_RISK_FLG_IND").HasFilter("\"FLG_IND\"");
        builder.HasAuditColumns();
    }
}
