using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Warehouse.Domain.Bins;

namespace Romp.Modules.Warehouse.Infrastructure.Bins;

internal sealed class BinConfiguration : IEntityTypeConfiguration<Bin>
{
    public void Configure(EntityTypeBuilder<Bin> builder)
    {
        builder.ToTable("BIN_MAIN");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(b => b.BinCode).HasColumnName("BIN_CODE").HasMaxLength(12).IsRequired();
        builder.Property(b => b.Zone).HasColumnName("ZONE").HasMaxLength(3).IsRequired();
        builder.Property(b => b.Aisle).HasColumnName("AISL").HasMaxLength(3).IsRequired();
        builder.Property(b => b.Shelf).HasColumnName("SHLF").HasMaxLength(2).IsRequired();
        builder.Property(b => b.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.Property(b => b.LastCountedAt).HasColumnName("LAST_CNT_DTE");

        // BIN-2: one bin per printed code. The database decides concurrent creates; the handler's check only gives a friendly message.
        builder.HasIndex(b => b.BinCode).IsUnique();
        builder.HasIndex(b => b.Zone);
        builder.HasAuditColumns();
    }
}
