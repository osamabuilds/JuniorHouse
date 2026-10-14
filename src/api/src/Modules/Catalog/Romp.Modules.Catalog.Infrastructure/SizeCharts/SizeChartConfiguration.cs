using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Catalog.Domain.SizeCharts;

namespace Romp.Modules.Catalog.Infrastructure.SizeCharts;

internal sealed class SizeChartConfiguration : IEntityTypeConfiguration<SizeChart>
{
    public void Configure(EntityTypeBuilder<SizeChart> builder)
    {
        builder.ToTable("CHRT_MAIN");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.CategoryId).HasColumnName("CATG_ID").IsRequired();
        builder.Property(c => c.GenderId).HasColumnName("GNDR_ID").IsRequired();
        builder.Property(c => c.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();

        // Q-CHT-5: one chart per category and gender; Unisex is the fallback.
        builder.HasIndex(c => new { c.CategoryId, c.GenderId }).IsUnique();
        builder.HasIndex(c => c.GenderId);

        builder.HasMany(c => c.Lines).WithOne().HasForeignKey(l => l.ChartId)
            .HasConstraintName("FK_CHRT_LINE_CHRT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(SizeChart.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.HasAuditColumns();
    }
}

internal sealed class SizeChartLineConfiguration : IEntityTypeConfiguration<SizeChartLine>
{
    public void Configure(EntityTypeBuilder<SizeChartLine> builder)
    {
        builder.ToTable("CHRT_LINE", table =>
        {
            // CHT-4: the database refuses a minimum above its maximum and any negative value, whatever wrote the row.
            table.HasCheckConstraint(
                "CK_CHRT_LINE_RANGE",
                "(\"AGE_MIN_MON\" IS NULL OR \"AGE_MAX_MON\" IS NULL OR \"AGE_MIN_MON\" <= \"AGE_MAX_MON\")"
                + " AND (\"HGHT_MIN_CM\" IS NULL OR \"HGHT_MAX_CM\" IS NULL OR \"HGHT_MIN_CM\" <= \"HGHT_MAX_CM\")"
                + " AND (\"WGHT_MIN_KG\" IS NULL OR \"WGHT_MAX_KG\" IS NULL OR \"WGHT_MIN_KG\" <= \"WGHT_MAX_KG\")");
            table.HasCheckConstraint(
                "CK_CHRT_LINE_POSITIVE",
                "COALESCE(\"AGE_MIN_MON\", 0) >= 0 AND COALESCE(\"AGE_MAX_MON\", 0) >= 0 AND COALESCE(\"HGHT_MIN_CM\", 0) >= 0"
                + " AND COALESCE(\"HGHT_MAX_CM\", 0) >= 0 AND COALESCE(\"WGHT_MIN_KG\", 0) >= 0 AND COALESCE(\"WGHT_MAX_KG\", 0) >= 0"
                + " AND COALESCE(\"CHST_CM\", 0) >= 0 AND COALESCE(\"WAIS_CM\", 0) >= 0 AND COALESCE(\"LEN_CM\", 0) >= 0");
        });
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.ChartId).HasColumnName("CHRT_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.AgeMinMonths).HasColumnName("AGE_MIN_MON");
        builder.Property(l => l.AgeMaxMonths).HasColumnName("AGE_MAX_MON");
        builder.Property(l => l.HeightMinCm).HasColumnName("HGHT_MIN_CM").HasColumnType("numeric(6,2)");
        builder.Property(l => l.HeightMaxCm).HasColumnName("HGHT_MAX_CM").HasColumnType("numeric(6,2)");
        builder.Property(l => l.WeightMinKg).HasColumnName("WGHT_MIN_KG").HasColumnType("numeric(6,2)");
        builder.Property(l => l.WeightMaxKg).HasColumnName("WGHT_MAX_KG").HasColumnType("numeric(6,2)");
        builder.Property(l => l.ChestCm).HasColumnName("CHST_CM").HasColumnType("numeric(6,2)");
        builder.Property(l => l.WaistCm).HasColumnName("WAIS_CM").HasColumnType("numeric(6,2)");
        builder.Property(l => l.LengthCm).HasColumnName("LEN_CM").HasColumnType("numeric(6,2)");

        // CHT-5: one line per size in a chart.
        builder.HasIndex(l => new { l.ChartId, l.SizeId }).IsUnique();
        builder.HasAuditColumns();
    }
}
