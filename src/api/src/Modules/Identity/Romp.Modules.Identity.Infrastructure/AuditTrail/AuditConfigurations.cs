using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Identity.Domain.AuditTrail;

namespace Romp.Modules.Identity.Infrastructure.AuditTrail;

/// <summary>IDNT.AUDT_OPRN_LKP row. The key is the enum, so the foreign key from AUDT_ENTY needs no conversion.</summary>
internal sealed class AuditOperationRow
{
    public AuditOperation Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

internal sealed class AuditOperationConfiguration : IEntityTypeConfiguration<AuditOperationRow>
{
    public void Configure(EntityTypeBuilder<AuditOperationRow> builder)
    {
        builder.ToTable("AUDT_OPRN_LKP");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").HasConversion<short>().ValueGeneratedNever();
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(r => r.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(
            new AuditOperationRow { Id = AuditOperation.Insert, Code = "INSERT", Name = "Created", SortSeq = 1, IsActive = true },
            new AuditOperationRow { Id = AuditOperation.Update, Code = "UPDATE", Name = "Changed", SortSeq = 2, IsActive = true },
            new AuditOperationRow { Id = AuditOperation.Delete, Code = "DELETE", Name = "Deleted", SortSeq = 3, IsActive = true });
    }
}

/// <summary>
/// AUDT_ENTY is range-partitioned by month on OCCR_DTE and guarded by triggers that reject UPDATE, DELETE and TRUNCATE. The migration
/// creates the partitioning, the triggers and the partition function in raw SQL; this mapping only describes the columns and keys so
/// EF can read and insert rows. The audit tables carry no INSR_/UPDT_ columns or concurrency token: they are append-only, and OCCR_DTE
/// and ACTR_NAME already say when and by whom.
/// </summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AUDT_ENTY");
        builder.HasKey(e => new { e.Id, e.OccurredAt });
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd().HasDefaultValueSql("nextval('\"IDNT\".\"AUDT_ENTY_ID_SEQ\"')");
        builder.Property(e => e.OccurredAt).HasColumnName("OCCR_DTE").ValueGeneratedNever();
        builder.Property(e => e.Module).HasColumnName("MODL_NAME").HasMaxLength(AuditEntry.ModuleMaxLength).IsRequired();
        builder.Property(e => e.SourceMessageId).HasColumnName("SRC_MSG_ID").IsRequired();
        builder.Property(e => e.EntityType).HasColumnName("ENTY_TYP_NAME").HasMaxLength(AuditEntry.EntityTypeMaxLength).IsRequired();
        builder.Property(e => e.EntityKey).HasColumnName("ENTY_KEY").HasMaxLength(AuditEntry.EntityKeyMaxLength).IsRequired();
        builder.Property(e => e.Operation).HasColumnName("OPRN_ID").HasConversion<short>().IsRequired();
        builder.Property(e => e.Actor).HasColumnName("ACTR_NAME").HasMaxLength(AuditEntry.ActorMaxLength).IsRequired();
        builder.Property(e => e.CorrelationId).HasColumnName("CORR_ID").HasMaxLength(AuditEntry.CorrelationMaxLength);

        // A redelivered message must not store a second entry (AC-5); OCCR_DTE is part of every unique key on a partitioned table.
        builder.HasIndex(e => new { e.Module, e.SourceMessageId, e.EntityType, e.EntityKey, e.OccurredAt }).IsUnique().HasDatabaseName("UX_AUDT_ENTY_SRC");

        // The list filters: newest first, by person, by record, by module.
        builder.HasIndex(e => e.OccurredAt).HasDatabaseName("IX_AUDT_ENTY_OCCR_DTE");
        builder.HasIndex(e => new { e.Actor, e.OccurredAt }).HasDatabaseName("IX_AUDT_ENTY_ACTR_NAME");
        builder.HasIndex(e => new { e.EntityType, e.EntityKey, e.OccurredAt }).HasDatabaseName("IX_AUDT_ENTY_ENTY_KEY");
        builder.HasIndex(e => new { e.Module, e.OccurredAt }).HasDatabaseName("IX_AUDT_ENTY_MODL_NAME");
        builder.HasIndex(e => e.Operation).HasDatabaseName("IX_AUDT_ENTY_OPRN_ID");

        builder.HasOne<AuditOperationRow>().WithMany().HasForeignKey(e => e.Operation)
            .HasConstraintName("FK_AUDT_ENTY_OPRN_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Columns).WithOne().HasForeignKey(c => new { c.EntryId, c.OccurredAt })
            .HasConstraintName("FK_AUDT_CHNG_ENTY").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AuditColumnConfiguration : IEntityTypeConfiguration<AuditColumn>
{
    public void Configure(EntityTypeBuilder<AuditColumn> builder)
    {
        builder.ToTable("AUDT_CHNG");
        builder.HasKey(c => new { c.Id, c.OccurredAt });
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd().HasDefaultValueSql("nextval('\"IDNT\".\"AUDT_CHNG_ID_SEQ\"')");
        builder.Property(c => c.EntryId).HasColumnName("ENTY_ID").IsRequired();
        builder.Property(c => c.OccurredAt).HasColumnName("OCCR_DTE").ValueGeneratedNever();
        builder.Property(c => c.Name).HasColumnName("COLM_NAME").HasMaxLength(AuditEntry.ColumnMaxLength).IsRequired();

        // The foreign key columns lead this index, so the join from an entry to its columns is index-backed.
        builder.HasIndex(c => new { c.EntryId, c.OccurredAt, c.Name }).IsUnique().HasDatabaseName("UX_AUDT_CHNG_COLM");
    }
}
