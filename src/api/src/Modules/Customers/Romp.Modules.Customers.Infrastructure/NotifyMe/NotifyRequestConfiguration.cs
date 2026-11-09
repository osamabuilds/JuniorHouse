using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Customers.Domain.NotifyMe;

namespace Romp.Modules.Customers.Infrastructure.NotifyMe;

/// <summary>CUST.NTFM_STS_LKP row. The key is the enum so the foreign key needs no conversion.</summary>
internal sealed class NotifyStatusRow
{
    public NotifyStatus Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>Seed rows of the Notify Me status lookup, written into the <c>AddNotifyMe</c> migration. Ids are the enum values and are never reused.</summary>
internal static class NotifyMeSeed
{
    public static IReadOnlyList<NotifyStatusRow> Statuses { get; } =
    [
        new() { Id = NotifyStatus.Open, Code = "OPEN", Name = "Open", SortSeq = 1, IsActive = true },
        new() { Id = NotifyStatus.Notified, Code = "NTFD", Name = "Notified", SortSeq = 2, IsActive = true },
        new() { Id = NotifyStatus.Expired, Code = "EXPD", Name = "Expired", SortSeq = 3, IsActive = true },
    ];
}

internal sealed class NotifyStatusConfiguration : IEntityTypeConfiguration<NotifyStatusRow>
{
    public void Configure(EntityTypeBuilder<NotifyStatusRow> builder)
    {
        builder.ToTable("NTFM_STS_LKP");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").HasConversion<short>().ValueGeneratedNever();
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(r => r.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(NotifyMeSeed.Statuses);
    }
}

internal sealed class NotifyRequestConfiguration : IEntityTypeConfiguration<NotifyRequest>
{
    public void Configure(EntityTypeBuilder<NotifyRequest> builder)
    {
        // The address is stored normalised (lower case), so the partial unique index is "one open request per variant and address" (NTM-3);
        // the check constraint keeps any other writer honest. VRNT_ID is a Labelling variant id: another module's id, so no foreign key.
        builder.ToTable("NTFM_REQ", table => table.HasCheckConstraint("CK_NTFM_REQ_EML_LOWR", "\"EML\" = lower(\"EML\")"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.VariantId).HasColumnName("VRNT_ID").IsRequired();
        builder.Property(r => r.Email).HasColumnName("EML").HasMaxLength(NotifyEmail.MaxLength).IsRequired();
        builder.Property(r => r.Status).HasColumnName("STS_ID").HasConversion<short>().IsRequired();
        builder.Property(r => r.NotifiedAt).HasColumnName("NTFD_DTE");
        builder.Property(r => r.SentAt).HasColumnName("SENT_DTE");
        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => new { r.VariantId, r.Email }, "UQ_NTFM_REQ_VRNT_ID_EML_OPEN").IsUnique().HasFilter("\"STS_ID\" = 1");
        builder.HasIndex(r => r.Status, "IX_NTFM_REQ_STS_ID");
        builder.HasIndex(r => new { r.VariantId, r.Status }, "IX_NTFM_REQ_VRNT_ID_STS_ID");

        builder.HasOne<NotifyStatusRow>().WithMany().HasForeignKey(r => r.Status)
            .HasConstraintName("FK_NTFM_REQ_STS_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasAuditColumns();
    }
}
