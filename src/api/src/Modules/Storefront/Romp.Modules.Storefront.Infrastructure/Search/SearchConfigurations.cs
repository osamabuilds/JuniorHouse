using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Storefront.Domain.Search;

namespace Romp.Modules.Storefront.Infrastructure.Search;

// Names follow docs/db/naming.md. SRCH_VEC (the weighted tsvector over PRDT_MAIN) is a generated column created by the migration; it is
// deliberately not in the EF model because only the search query reads it, through SQL.

internal sealed class SynonymGroupConfiguration : IEntityTypeConfiguration<SynonymGroup>
{
    public void Configure(EntityTypeBuilder<SynonymGroup> builder)
    {
        builder.ToTable("SRCH_SYNM_GRP");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(g => g.Name).HasColumnName("NAME").HasMaxLength(60).IsRequired();
        builder.Property(g => g.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasIndex(g => g.Name).IsUnique();
        builder.HasAuditColumns();
    }
}

internal sealed class SynonymTermConfiguration : IEntityTypeConfiguration<SynonymTerm>
{
    public void Configure(EntityTypeBuilder<SynonymTerm> builder)
    {
        builder.ToTable("SRCH_SYNM_TERM");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(t => t.GroupId).HasColumnName("GRP_ID").IsRequired();
        builder.Property(t => t.Term).HasColumnName("TERM").HasMaxLength(60).IsRequired();
        builder.Property(t => t.IsPhrase).HasColumnName("PHRS_IND").IsRequired();

        // A term belongs to one group, so a search never expands two ways.
        builder.HasIndex(t => t.Term).IsUnique();
        builder.HasIndex(t => t.GroupId);
        builder.HasOne<SynonymGroup>().WithMany().HasForeignKey(t => t.GroupId)
            .HasConstraintName("FK_SRCH_SYNM_TERM_GRP_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class SearchMissConfiguration : IEntityTypeConfiguration<SearchMiss>
{
    public void Configure(EntityTypeBuilder<SearchMiss> builder)
    {
        builder.ToTable("SRCH_MISS", table => table.HasCheckConstraint("CK_SRCH_MISS_CNT_POS", "\"MISS_CNT\" > 0"));
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(m => m.QueryText).HasColumnName("QRY_TEXT").HasMaxLength(100).IsRequired();
        builder.Property(m => m.MissDate).HasColumnName("MISS_DT").IsRequired();
        builder.Property(m => m.MissCount).HasColumnName("MISS_CNT").IsRequired();

        // One row per distinct text per day; the upsert increments it.
        builder.HasIndex(m => new { m.QueryText, m.MissDate }).IsUnique();
        // The purge and the staff list read by day.
        builder.HasIndex(m => m.MissDate);
        builder.HasAuditColumns();
    }
}
