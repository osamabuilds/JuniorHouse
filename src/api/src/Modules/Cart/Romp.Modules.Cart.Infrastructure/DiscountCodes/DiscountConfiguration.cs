using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Cart.Domain.DiscountCodes;

namespace Romp.Modules.Cart.Infrastructure.DiscountCodes;

/// <summary>CART.DISC_TYPE_LKP row (standard lookup shape).</summary>
internal sealed class DiscountKindRow
{
    public DiscountKind Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>CART.RDMP_STS_LKP row (standard lookup shape).</summary>
internal sealed class RedemptionStatusRow
{
    public RedemptionStatus Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>Seed rows of the two discount lookups, written into the migration. Ids are the enum values and are never reused.</summary>
internal static class DiscountSeed
{
    public static IReadOnlyList<DiscountKindRow> Kinds { get; } =
    [
        new() { Id = DiscountKind.Percentage, Code = "PCT", Name = "Percentage", Description = "A percentage of the eligible subtotal", SortSeq = 1, IsActive = true },
        new() { Id = DiscountKind.Fixed, Code = "FIXD", Name = "Fixed amount", Description = "A fixed amount in rupees", SortSeq = 2, IsActive = true },
    ];

    public static IReadOnlyList<RedemptionStatusRow> Statuses { get; } =
    [
        new() { Id = RedemptionStatus.Held, Code = "HELD", Name = "Held", Description = "Kept for an order that is being placed", SortSeq = 1, IsActive = true },
        new() { Id = RedemptionStatus.Confirmed, Code = "CNFM", Name = "Confirmed", Description = "Used by a placed order", SortSeq = 2, IsActive = true },
        new() { Id = RedemptionStatus.Released, Code = "RLSD", Name = "Released", Description = "Given back: the order never came or was cancelled", SortSeq = 3, IsActive = true },
    ];
}

internal sealed class DiscountKindConfiguration : IEntityTypeConfiguration<DiscountKindRow>
{
    public void Configure(EntityTypeBuilder<DiscountKindRow> builder)
    {
        builder.ToTable("DISC_TYPE_LKP");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").HasConversion<short>().ValueGeneratedNever();
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(r => r.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(DiscountSeed.Kinds);
    }
}

internal sealed class RedemptionStatusConfiguration : IEntityTypeConfiguration<RedemptionStatusRow>
{
    public void Configure(EntityTypeBuilder<RedemptionStatusRow> builder)
    {
        builder.ToTable("RDMP_STS_LKP");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").HasConversion<short>().ValueGeneratedNever();
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(r => r.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(DiscountSeed.Statuses);
    }
}

internal sealed class DiscountCodeConfiguration : IEntityTypeConfiguration<DiscountCode>
{
    public void Configure(EntityTypeBuilder<DiscountCode> builder)
    {
        builder.ToTable("DISC_CODE", table =>
        {
            table.HasCheckConstraint("CK_DISC_CODE_VAL", "\"VAL\" > 0 AND (\"DISC_TYPE_ID\" <> 1 OR \"VAL\" <= 100)");
            table.HasCheckConstraint("CK_DISC_CODE_LIMITS", "(\"USE_CAP\" IS NULL OR \"USE_CAP\" >= 1) AND (\"CUST_USE_CAP\" IS NULL OR \"CUST_USE_CAP\" >= 1) AND (\"MIN_SPND_AMT\" IS NULL OR \"MIN_SPND_AMT\" >= 0)");
        });
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.Code).HasColumnName("CODE").HasMaxLength(DiscountCode.MaxCodeLength).IsRequired();
        builder.Property(c => c.Kind).HasColumnName("DISC_TYPE_ID").HasConversion<short>().IsRequired();
        builder.Property(c => c.Value).HasColumnName("VAL").HasPrecision(12, 2).IsRequired();
        builder.Property(c => c.MinimumSpend).HasColumnName("MIN_SPND_AMT").HasPrecision(12, 2);
        builder.Property(c => c.ExpiresAt).HasColumnName("EXPY_DTE");
        builder.Property(c => c.UsageCap).HasColumnName("USE_CAP");
        builder.Property(c => c.PerCustomerCap).HasColumnName("CUST_USE_CAP");
        builder.Property(c => c.IncludesSaleItems).HasColumnName("SALE_INCL_IND").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("ACT_IND").IsRequired();

        // The code is stored upper-case, so one unique index makes it unique however it was typed (DSC-12).
        builder.HasIndex(c => c.Code, "UQ_DISC_CODE_CODE").IsUnique();
        builder.HasOne<DiscountKindRow>().WithMany().HasForeignKey(c => c.Kind).HasConstraintName("FK_DISC_CODE_DISC_TYPE_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(c => c.Scope).WithOne().HasForeignKey(s => s.DiscountCodeId).HasConstraintName("FK_DISC_SCOP_DISC_CODE_ID").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Scope).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(c => c.DomainEvents);
        builder.HasAuditColumns();
    }
}

internal sealed class DiscountScopeConfiguration : IEntityTypeConfiguration<DiscountScopeCategory>
{
    public void Configure(EntityTypeBuilder<DiscountScopeCategory> builder)
    {
        builder.ToTable("DISC_SCOP");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.DiscountCodeId).HasColumnName("DISC_CODE_ID").IsRequired();
        builder.Property(s => s.CategoryId).HasColumnName("CTGY_ID").IsRequired();

        // A category appears once per code. The category id is a plain reference to Catalog (ADR 0002).
        builder.HasIndex(s => new { s.DiscountCodeId, s.CategoryId }, "UQ_DISC_SCOP_DISC_CODE_ID_CTGY_ID").IsUnique();
        builder.HasAuditColumns();
    }
}

internal sealed class RedemptionConfiguration : IEntityTypeConfiguration<Redemption>
{
    public void Configure(EntityTypeBuilder<Redemption> builder)
    {
        builder.ToTable("DISC_RDMP", table => table.HasCheckConstraint("CK_DISC_RDMP_AMT", "\"AMT\" >= 0"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.DiscountCodeId).HasColumnName("DISC_CODE_ID").IsRequired();
        builder.Property(r => r.PlacementKey).HasColumnName("PLCE_KEY").HasMaxLength(64).IsRequired();
        builder.Property(r => r.CustomerKey).HasColumnName("CUST_KEY").HasMaxLength(70).IsRequired();
        builder.Property(r => r.Amount).HasColumnName("AMT").HasPrecision(12, 2).IsRequired();
        builder.Property(r => r.Status).HasColumnName("RDMP_STS_ID").HasConversion<short>().IsRequired();
        builder.Property(r => r.OrderNumber).HasColumnName("ORDR_NO").HasMaxLength(30);
        builder.Ignore(r => r.CountsAsUse);

        // One redemption per placement key (DSC-10): a repeated hold is the same hold.
        builder.HasIndex(r => r.PlacementKey, "UQ_DISC_RDMP_PLCE_KEY").IsUnique();
        builder.HasIndex(r => new { r.DiscountCodeId, r.CustomerKey }, "IX_DISC_RDMP_DISC_CODE_ID_CUST_KEY");
        builder.HasIndex(r => new { r.Status, r.InsrDte }, "IX_DISC_RDMP_RDMP_STS_ID_INSR_DTE");
        builder.HasOne<DiscountCode>().WithMany().HasForeignKey(r => r.DiscountCodeId).HasConstraintName("FK_DISC_RDMP_DISC_CODE_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RedemptionStatusRow>().WithMany().HasForeignKey(r => r.Status).HasConstraintName("FK_DISC_RDMP_RDMP_STS_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasAuditColumns();
    }
}
