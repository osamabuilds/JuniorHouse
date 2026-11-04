using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Cart.Domain.Totals;

namespace Romp.Modules.Cart.Infrastructure.Totals;

internal sealed class ShippingCityRateConfiguration : IEntityTypeConfiguration<ShippingCityRate>
{
    public void Configure(EntityTypeBuilder<ShippingCityRate> builder)
    {
        builder.ToTable("SHPG_CITY_RATE", table => table.HasCheckConstraint("CK_SHPG_CITY_RATE_FEE", "\"FEE_AMT\" >= 0"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.CityId).HasColumnName("CITY_ID").IsRequired();
        builder.Property(r => r.Fee).HasColumnName("FEE_AMT").HasPrecision(12, 2).IsRequired();

        // One rate per city. The city id is a plain reference into the Reference lookups (no key crosses a schema, ADR 0002).
        builder.HasIndex(r => r.CityId, "UQ_SHPG_CITY_RATE_CITY_ID").IsUnique();
        builder.HasAuditColumns();
    }
}

internal sealed class ShippingSettingsConfiguration : IEntityTypeConfiguration<ShippingSettings>
{
    public void Configure(EntityTypeBuilder<ShippingSettings> builder)
    {
        builder.ToTable("SHPG_SETG", table =>
        {
            table.HasCheckConstraint("CK_SHPG_SETG_ONE", "\"ID\" = 1");
            table.HasCheckConstraint("CK_SHPG_SETG_FEES", "\"DFLT_FEE_AMT\" >= 0 AND (\"COD_FEE_AMT\" IS NULL OR \"COD_FEE_AMT\" >= 0) AND (\"FREE_THRS_AMT\" IS NULL OR \"FREE_THRS_AMT\" > 0)");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(s => s.DefaultFee).HasColumnName("DFLT_FEE_AMT").HasPrecision(12, 2).IsRequired();
        builder.Property(s => s.FreeThreshold).HasColumnName("FREE_THRS_AMT").HasPrecision(12, 2);
        builder.Property(s => s.CodFee).HasColumnName("COD_FEE_AMT").HasPrecision(12, 2);

        // The one row exists from the first migration, with a zero default fee the business must set (health warning until then).
        builder.HasData(new { Id = ShippingSettings.TheOne, DefaultFee = 0m, InsrBy = "migration", InsrDte = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero) });
        builder.HasAuditColumns();
    }
}
