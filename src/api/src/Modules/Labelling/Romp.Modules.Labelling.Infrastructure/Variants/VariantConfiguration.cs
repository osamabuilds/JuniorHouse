using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Infrastructure.Variants;

internal sealed class VariantConfiguration : IEntityTypeConfiguration<Variant>
{
    public void Configure(EntityTypeBuilder<Variant> builder)
    {
        builder.ToTable("VRNT_MAIN");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(v => v.StyleId).HasColumnName("STYL_ID").IsRequired();
        builder.Property(v => v.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(v => v.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(v => v.SkuCode).HasColumnName("SKU_CODE").HasMaxLength(120).IsRequired();

        // FR-SC-06: one variant per style x colour x size, one SKU code per variant (the database is the final guard, ADR 0009).
        builder.HasIndex(v => new { v.StyleId, v.ColourId, v.SizeId }).IsUnique();
        builder.HasIndex(v => v.SkuCode).IsUnique();
        builder.HasAuditColumns();

        builder.Metadata.FindNavigation(nameof(Variant.Gtins))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class VariantGtinConfiguration : IEntityTypeConfiguration<VariantGtin>
{
    public void Configure(EntityTypeBuilder<VariantGtin> builder)
    {
        builder.ToTable("VRNT_GTIN");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(g => g.VariantId).HasColumnName("VRNT_ID").IsRequired();
        builder.Property(g => g.Gtin).HasColumnName("GTIN").HasMaxLength(13).IsFixedLength().IsRequired();
        builder.Property(g => g.Prefix).HasColumnName("PFX").HasMaxLength(9).IsRequired();
        builder.Property(g => g.ItemReference).HasColumnName("ITEM_REF").IsRequired();
        builder.Property(g => g.StatusId).HasColumnName("GTIN_STS_ID").IsRequired();
        builder.Property(g => g.RetiredAt).HasColumnName("RETD_DTE");
        builder.Property(g => g.RetiredReasonId).HasColumnName("RETD_RSN_ID");
        builder.Property(g => g.RetiredNote).HasColumnName("RETD_NOTE").HasMaxLength(200);
        builder.Property(g => g.RetiredBy).HasColumnName("RETD_BY").HasMaxLength(100);

        // NFR-SC-03: a GTIN and an (prefix, item reference) pair exist once, ever. Retired rows stay, so a number is never reissued.
        builder.HasIndex(g => g.Gtin).IsUnique();
        builder.HasIndex(g => new { g.Prefix, g.ItemReference }).IsUnique();

        // VAR-3: at most ONE active GTIN per variant, enforced by the database and not only by code (partial unique index).
        builder.HasIndex(g => g.VariantId).IsUnique().HasFilter($"\"GTIN_STS_ID\" = {GtinStatus.Active}").HasDatabaseName("UX_VRNT_GTIN_ACT");
        builder.HasIndex(g => new { g.VariantId, g.StatusId }).HasDatabaseName("IX_VRNT_GTIN_VRNT_STS");
        builder.HasAuditColumns();

        builder.HasOne<Variant>().WithMany(v => v.Gtins).HasForeignKey(g => g.VariantId)
            .HasConstraintName("FK_VRNT_GTIN_VRNT_MAIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GtinStatusLookup>().WithMany().HasForeignKey(g => g.StatusId).HasConstraintName("FK_VRNT_GTIN_GTIN_STS_LKP");
        builder.HasOne<VoidReasonLookup>().WithMany().HasForeignKey(g => g.RetiredReasonId).HasConstraintName("FK_VRNT_GTIN_VOID_RSN_LKP");
    }
}

internal sealed class GtinNumberSequenceConfiguration : IEntityTypeConfiguration<GtinNumberSequence>
{
    public void Configure(EntityTypeBuilder<GtinNumberSequence> builder)
    {
        builder.ToTable("GTIN_NO_SEQ");
        builder.HasKey(s => s.Prefix);
        builder.Property(s => s.Prefix).HasColumnName("PFX").HasMaxLength(9);
        builder.Property(s => s.Seq).HasColumnName("SEQ").IsRequired();
    }
}
