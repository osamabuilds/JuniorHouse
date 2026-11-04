using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Customers.Domain.Newsletter;

namespace Romp.Modules.Customers.Infrastructure.Newsletter;

/// <summary>CUST.NWSL_STS_LKP row. The key is the enum so the foreign key needs no conversion.</summary>
internal sealed class NewsletterStatusRow
{
    public NewsletterStatus Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>CUST.NWSL_ACTN_LKP row.</summary>
internal sealed class ConsentActionRow
{
    public ConsentAction Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>CUST.NWSL_SRC_LKP row.</summary>
internal sealed class ConsentSourceRow
{
    public ConsentSource Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>Seed rows of the three newsletter lookups, written into the <c>AddNewsletter</c> migration. Ids are the enum values and are never reused.</summary>
internal static class NewsletterSeed
{
    public static IReadOnlyList<NewsletterStatusRow> Statuses { get; } =
    [
        new() { Id = NewsletterStatus.Subscribed, Code = "SUBD", Name = "Subscribed", SortSeq = 1, IsActive = true },
        new() { Id = NewsletterStatus.Unsubscribed, Code = "UNSB", Name = "Unsubscribed", SortSeq = 2, IsActive = true },
    ];

    public static IReadOnlyList<ConsentActionRow> Actions { get; } =
    [
        new() { Id = ConsentAction.Subscribed, Code = "SUBD", Name = "Subscribed", SortSeq = 1, IsActive = true },
        new() { Id = ConsentAction.Unsubscribed, Code = "UNSB", Name = "Unsubscribed", SortSeq = 2, IsActive = true },
    ];

    public static IReadOnlyList<ConsentSourceRow> Sources { get; } =
    [
        new() { Id = ConsentSource.Registration, Code = "REGN", Name = "Registration form", SortSeq = 1, IsActive = true },
        new() { Id = ConsentSource.Account, Code = "ACCT", Name = "Account preferences", SortSeq = 2, IsActive = true },
        new() { Id = ConsentSource.Footer, Code = "FOOT", Name = "Footer form", SortSeq = 3, IsActive = true },
        new() { Id = ConsentSource.Link, Code = "LINK", Name = "Link in an email", SortSeq = 4, IsActive = true },
    ];
}

internal sealed class NewsletterStatusConfiguration : IEntityTypeConfiguration<NewsletterStatusRow>
{
    public void Configure(EntityTypeBuilder<NewsletterStatusRow> builder)
    {
        builder.ToTable("NWSL_STS_LKP");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").HasConversion<short>().ValueGeneratedNever();
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(r => r.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(NewsletterSeed.Statuses);
    }
}

internal sealed class ConsentActionConfiguration : IEntityTypeConfiguration<ConsentActionRow>
{
    public void Configure(EntityTypeBuilder<ConsentActionRow> builder)
    {
        builder.ToTable("NWSL_ACTN_LKP");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").HasConversion<short>().ValueGeneratedNever();
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(r => r.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(NewsletterSeed.Actions);
    }
}

internal sealed class ConsentSourceConfiguration : IEntityTypeConfiguration<ConsentSourceRow>
{
    public void Configure(EntityTypeBuilder<ConsentSourceRow> builder)
    {
        builder.ToTable("NWSL_SRC_LKP");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").HasConversion<short>().ValueGeneratedNever();
        builder.Property(r => r.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(r => r.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(NewsletterSeed.Sources);
    }
}

internal sealed class NewsletterSubscriptionConfiguration : IEntityTypeConfiguration<NewsletterSubscription>
{
    public void Configure(EntityTypeBuilder<NewsletterSubscription> builder)
    {
        // The address is stored normalised (lower case), so a plain unique index is "once per email, case-insensitive" (NLS-1);
        // the check constraint keeps any other writer honest.
        builder.ToTable("NWSL_SUBS", table => table.HasCheckConstraint("CK_NWSL_SUBS_EML_LOWR", "\"EML\" = lower(\"EML\")"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.Email).HasColumnName("EML").HasMaxLength(NewsletterEmail.MaxLength).IsRequired();
        builder.Property(s => s.Status).HasColumnName("STS_ID").HasConversion<short>().IsRequired();

        builder.Property(s => s.ChangedAt).HasColumnName("CHNG_DTE").IsRequired();
        builder.Property(s => s.WelcomeSentAt).HasColumnName("WLCM_SENT_DTE");
        builder.Ignore(s => s.IsSubscribed);
        builder.Ignore(s => s.DomainEvents);

        builder.HasIndex(s => s.Email, "UQ_NWSL_SUBS_EML").IsUnique();
        builder.HasIndex(s => s.Status, "IX_NWSL_SUBS_STS_ID");

        builder.HasOne<NewsletterStatusRow>().WithMany().HasForeignKey(s => s.Status)
            .HasConstraintName("FK_NWSL_SUBS_STS_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.History).WithOne().HasForeignKey(h => h.SubscriptionId)
            .HasConstraintName("FK_NWSL_HIST_SUBS_ID").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasAuditColumns();
    }
}

internal sealed class ConsentEventConfiguration : IEntityTypeConfiguration<ConsentEvent>
{
    public void Configure(EntityTypeBuilder<ConsentEvent> builder)
    {
        builder.ToTable("NWSL_HIST");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(h => h.SubscriptionId).HasColumnName("SUBS_ID").IsRequired();
        builder.Property(h => h.Action).HasColumnName("ACTN_ID").HasConversion<short>().IsRequired();
        builder.Property(h => h.Source).HasColumnName("SRC_ID").HasConversion<short>().IsRequired();
        builder.Property(h => h.OccurredAt).HasColumnName("OCCR_DTE").IsRequired();

        builder.HasIndex(h => h.SubscriptionId, "IX_NWSL_HIST_SUBS_ID");
        builder.HasIndex(h => h.Action, "IX_NWSL_HIST_ACTN_ID");
        builder.HasIndex(h => h.Source, "IX_NWSL_HIST_SRC_ID");

        builder.HasOne<ConsentActionRow>().WithMany().HasForeignKey(h => h.Action)
            .HasConstraintName("FK_NWSL_HIST_ACTN_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConsentSourceRow>().WithMany().HasForeignKey(h => h.Source)
            .HasConstraintName("FK_NWSL_HIST_SRC_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasAuditColumns();
    }
}
