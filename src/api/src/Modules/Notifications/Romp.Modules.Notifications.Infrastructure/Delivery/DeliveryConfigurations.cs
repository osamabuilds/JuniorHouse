using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Infrastructure.Delivery;

/// <summary>
/// How the enumerated values are stored: each is a lookup id (smallint) pointing at a seeded lookup row. The id is the enum value plus one,
/// so ids start at 1 and never change meaning.
/// </summary>
internal static class DeliveryConversions
{
    public static short KindId(NotificationKind kind) => (short)((int)kind + 1);

    public static short ChannelId(NotificationChannel channel) => (short)((int)channel + 1);

    public static short StatusId(DeliveryStatus status) => (short)((int)status + 1);

    public static short ErrorId(ErrorCategory category) => (short)((int)category + 1);

    public static short ReferenceKindId(string code) => code == ReferenceKinds.Order
        ? (short)1
        : throw new InvalidOperationException($"Unknown reference kind '{code}'.");

    public static string ReferenceKindCode(short id) => id == 1
        ? ReferenceKinds.Order
        : throw new InvalidOperationException($"Unknown reference kind id {id}.");
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("NTFY_MAIN");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(n => n.Kind).HasColumnName("NTFY_KIND_ID").HasConversion(v => DeliveryConversions.KindId(v), v => (NotificationKind)(v - 1)).IsRequired();
        builder.Property(n => n.ReferenceKind).HasColumnName("REF_KIND_ID").HasConversion(v => DeliveryConversions.ReferenceKindId(v), v => DeliveryConversions.ReferenceKindCode(v)).IsRequired();
        builder.Property(n => n.ReferenceId).HasColumnName("REF_ID").IsRequired();
        builder.Property(n => n.EventId).HasColumnName("EVNT_ID").IsRequired();
        builder.Property(n => n.ExpiresAt).HasColumnName("EXPY_DTE").IsRequired();
        builder.Property(n => n.Priority).HasColumnName("PRIO").IsRequired();
        builder.HasAuditColumns();

        // One notification per business event and kind: a redelivered event creates nothing new (DLV-4).
        builder.HasIndex(n => new { n.EventId, n.Kind }).IsUnique().HasDatabaseName("UQ_NTFY_MAIN_EVNT_ID_NTFY_KIND_ID");
        builder.HasIndex(n => new { n.ReferenceKind, n.ReferenceId }).HasDatabaseName("IX_NTFY_MAIN_REF_KIND_ID_REF_ID");
        builder.HasIndex(n => n.Kind).HasDatabaseName("IX_NTFY_MAIN_NTFY_KIND_ID");

        builder.HasMany(n => n.Deliveries).WithOne().HasForeignKey(d => d.NotificationId)
            .HasConstraintName("FK_NTFY_CHNL_NTFY_MAIN").OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Notification.Deliveries))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<NotificationKindLookup>().WithMany().HasForeignKey(n => n.Kind).HasConstraintName("FK_NTFY_MAIN_NTFY_KIND_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReferenceKindLookup>().WithMany().HasForeignKey(n => n.ReferenceKind).HasConstraintName("FK_NTFY_MAIN_REF_KIND_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ChannelDeliveryConfiguration : IEntityTypeConfiguration<ChannelDelivery>
{
    public void Configure(EntityTypeBuilder<ChannelDelivery> builder)
    {
        builder.ToTable("NTFY_CHNL");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(d => d.NotificationId).HasColumnName("NTFY_ID").IsRequired();
        builder.Property(d => d.Channel).HasColumnName("CHNL_ID").HasConversion(v => DeliveryConversions.ChannelId(v), v => (NotificationChannel)(v - 1)).IsRequired();
        builder.Property(d => d.Status).HasColumnName("CHNL_STS_ID").HasConversion(v => DeliveryConversions.StatusId(v), v => (DeliveryStatus)(v - 1)).IsRequired();
        builder.Property(d => d.AttemptCount).HasColumnName("ATMP_CNT").IsRequired();
        builder.Property(d => d.NextAttemptAt).HasColumnName("NXT_ATMP_DTE");
        builder.Property(d => d.LeaseUntil).HasColumnName("LEAS_DTE");
        builder.Property(d => d.LastError).HasColumnName("ERR_CATG_ID").HasConversion(v => v.HasValue ? DeliveryConversions.ErrorId(v.Value) : (short?)null, v => v.HasValue ? (ErrorCategory)(v.Value - 1) : null);
        builder.Property(d => d.ProviderMessageId).HasColumnName("PRVD_MSG_ID").HasMaxLength(100);
        builder.Property(d => d.FallbackOfId).HasColumnName("FLBK_OF_ID");
        builder.Property(d => d.ResendKey).HasColumnName("RSND_KEY").HasMaxLength(64);
        builder.Ignore(d => d.IsFinal);
        builder.HasAuditColumns();

        // The original delivery on a channel is unique; resends and fallbacks (FLBK_OF_ID set) are not (DLV-4).
        builder.HasIndex(d => new { d.NotificationId, d.Channel }).IsUnique().HasFilter("\"FLBK_OF_ID\" IS NULL").HasDatabaseName("UQ_NTFY_CHNL_NTFY_ID_CHNL_ID");
        builder.HasIndex(d => d.NotificationId).HasDatabaseName("IX_NTFY_CHNL_NTFY_ID");
        // The worker's claim looks only at queued work by due time (DLV-9, DLV-11).
        builder.HasIndex(d => d.NextAttemptAt).HasFilter("\"CHNL_STS_ID\" = 1").HasDatabaseName("IX_NTFY_CHNL_NXT_ATMP_DTE_DUE");
        builder.HasIndex(d => d.ProviderMessageId).HasDatabaseName("IX_NTFY_CHNL_PRVD_MSG_ID");
        builder.HasIndex(d => d.Channel).HasDatabaseName("IX_NTFY_CHNL_CHNL_ID");
        builder.HasIndex(d => d.Status).HasDatabaseName("IX_NTFY_CHNL_CHNL_STS_ID");
        builder.HasIndex(d => d.LastError).HasDatabaseName("IX_NTFY_CHNL_ERR_CATG_ID");
        builder.HasIndex(d => d.FallbackOfId).HasDatabaseName("IX_NTFY_CHNL_FLBK_OF_ID");
        // One staff resend per client key: the database refuses a second even if two requests race (DLG-8).
        builder.HasIndex(d => d.ResendKey).IsUnique().HasFilter("\"RSND_KEY\" IS NOT NULL").HasDatabaseName("UQ_NTFY_CHNL_RSND_KEY");

        builder.HasOne<ChannelLookup>().WithMany().HasForeignKey(d => d.Channel).HasConstraintName("FK_NTFY_CHNL_CHNL_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DeliveryStatusLookup>().WithMany().HasForeignKey(d => d.Status).HasConstraintName("FK_NTFY_CHNL_CHNL_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErrorCategoryLookup>().WithMany().HasForeignKey(d => d.LastError).HasConstraintName("FK_NTFY_CHNL_ERR_CATG_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChannelDelivery>().WithMany().HasForeignKey(d => d.FallbackOfId).HasConstraintName("FK_NTFY_CHNL_FLBK_OF").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
    {
        builder.ToTable("NTFY_ATMP");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.DeliveryId).HasColumnName("NTFY_CHNL_ID").IsRequired();
        builder.Property(a => a.AttemptNumber).HasColumnName("ATMP_NO").IsRequired();
        builder.Property(a => a.Outcome).HasColumnName("CHNL_STS_ID").HasConversion(v => DeliveryConversions.StatusId(v), v => (DeliveryStatus)(v - 1)).IsRequired();
        builder.Property(a => a.Error).HasColumnName("ERR_CATG_ID").HasConversion(v => v.HasValue ? DeliveryConversions.ErrorId(v.Value) : (short?)null, v => v.HasValue ? (ErrorCategory)(v.Value - 1) : null);
        builder.Property(a => a.AttemptedAt).HasColumnName("ATMP_DTE").IsRequired();
        builder.HasAuditColumns();

        builder.HasIndex(a => new { a.DeliveryId, a.AttemptNumber }).HasDatabaseName("IX_NTFY_ATMP_NTFY_CHNL_ID_ATMP_NO");
        builder.HasIndex(a => a.Outcome).HasDatabaseName("IX_NTFY_ATMP_CHNL_STS_ID");
        builder.HasIndex(a => a.Error).HasDatabaseName("IX_NTFY_ATMP_ERR_CATG_ID");

        builder.HasOne<ChannelDelivery>().WithMany().HasForeignKey(a => a.DeliveryId).HasConstraintName("FK_NTFY_ATMP_NTFY_CHNL").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DeliveryStatusLookup>().WithMany().HasForeignKey(a => a.Outcome).HasConstraintName("FK_NTFY_ATMP_CHNL_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErrorCategoryLookup>().WithMany().HasForeignKey(a => a.Error).HasConstraintName("FK_NTFY_ATMP_ERR_CATG_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DeadLetterConfiguration : IEntityTypeConfiguration<DeadLetter>
{
    public void Configure(EntityTypeBuilder<DeadLetter> builder)
    {
        builder.ToTable("NTFY_DEDL");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(d => d.DeliveryId).HasColumnName("NTFY_CHNL_ID").IsRequired();
        builder.Property(d => d.Kind).HasColumnName("NTFY_KIND_ID").HasConversion(v => DeliveryConversions.KindId(v), v => (NotificationKind)(v - 1)).IsRequired();
        builder.Property(d => d.Channel).HasColumnName("CHNL_ID").HasConversion(v => DeliveryConversions.ChannelId(v), v => (NotificationChannel)(v - 1)).IsRequired();
        builder.Property(d => d.Reason).HasColumnName("ERR_CATG_ID").HasConversion(v => DeliveryConversions.ErrorId(v), v => (ErrorCategory)(v - 1)).IsRequired();
        builder.Property(d => d.AttemptCount).HasColumnName("ATMP_CNT").IsRequired();
        builder.Property(d => d.DeadAt).HasColumnName("DEDL_DTE").IsRequired();
        builder.Property(d => d.ResentBy).HasColumnName("RSND_BY").HasMaxLength(100);
        builder.Property(d => d.ResentAt).HasColumnName("RSND_DTE");
        builder.HasAuditColumns();

        builder.HasIndex(d => d.DeliveryId).IsUnique().HasDatabaseName("UQ_NTFY_DEDL_NTFY_CHNL_ID");
        builder.HasIndex(d => d.DeadAt).HasDatabaseName("IX_NTFY_DEDL_DEDL_DTE");
        builder.HasIndex(d => d.Kind).HasDatabaseName("IX_NTFY_DEDL_NTFY_KIND_ID");
        builder.HasIndex(d => d.Channel).HasDatabaseName("IX_NTFY_DEDL_CHNL_ID");
        builder.HasIndex(d => d.Reason).HasDatabaseName("IX_NTFY_DEDL_ERR_CATG_ID");

        builder.HasOne<ChannelDelivery>().WithMany().HasForeignKey(d => d.DeliveryId).HasConstraintName("FK_NTFY_DEDL_NTFY_CHNL").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<NotificationKindLookup>().WithMany().HasForeignKey(d => d.Kind).HasConstraintName("FK_NTFY_DEDL_NTFY_KIND_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChannelLookup>().WithMany().HasForeignKey(d => d.Channel).HasConstraintName("FK_NTFY_DEDL_CHNL_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErrorCategoryLookup>().WithMany().HasForeignKey(d => d.Reason).HasConstraintName("FK_NTFY_DEDL_ERR_CATG_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        builder.ToTable("NTFY_TMPL");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(t => t.Kind).HasColumnName("NTFY_KIND_ID").HasConversion(v => DeliveryConversions.KindId(v), v => (NotificationKind)(v - 1)).IsRequired();
        builder.Property(t => t.Channel).HasColumnName("CHNL_ID").HasConversion(v => DeliveryConversions.ChannelId(v), v => (NotificationChannel)(v - 1)).IsRequired();
        builder.Property(t => t.Language).HasColumnName("LANG").HasMaxLength(5).IsRequired();
        builder.Property(t => t.Version).HasColumnName("VER").IsRequired();
        builder.Property(t => t.Subject).HasColumnName("SUBJ").HasMaxLength(200).IsRequired();
        builder.Property(t => t.PlainText).HasColumnName("TEXT_BODY").HasMaxLength(4000).IsRequired();
        builder.Property(t => t.Html).HasColumnName("HTML_BODY").HasMaxLength(20000);
        builder.Property(t => t.ProviderTemplateName).HasColumnName("PRVD_TMPL_NAME").HasMaxLength(100);
        builder.Property(t => t.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasAuditColumns();

        builder.HasIndex(t => new { t.Kind, t.Channel, t.Language, t.Version }).IsUnique().HasDatabaseName("UQ_NTFY_TMPL_KIND_CHNL_LANG_VER");
        builder.HasIndex(t => t.Channel).HasDatabaseName("IX_NTFY_TMPL_CHNL_ID");

        builder.HasOne<NotificationKindLookup>().WithMany().HasForeignKey(t => t.Kind).HasConstraintName("FK_NTFY_TMPL_NTFY_KIND_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChannelLookup>().WithMany().HasForeignKey(t => t.Channel).HasConstraintName("FK_NTFY_TMPL_CHNL_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DailySendCountConfiguration : IEntityTypeConfiguration<DailySendCount>
{
    public void Configure(EntityTypeBuilder<DailySendCount> builder)
    {
        builder.ToTable("NTFY_DAY_CNT");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.Day).HasColumnName("DAY_DT").IsRequired();
        builder.Property(c => c.Channel).HasColumnName("CHNL_ID").HasConversion(v => DeliveryConversions.ChannelId(v), v => (NotificationChannel)(v - 1)).IsRequired();
        builder.Property(c => c.SentCount).HasColumnName("SENT_CNT").IsRequired();
        builder.HasAuditColumns();

        builder.HasIndex(c => new { c.Day, c.Channel }).IsUnique().HasDatabaseName("UQ_NTFY_DAY_CNT_DAY_DT_CHNL_ID");
        builder.HasIndex(c => c.Channel).HasDatabaseName("IX_NTFY_DAY_CNT_CHNL_ID");

        builder.HasOne<ChannelLookup>().WithMany().HasForeignKey(c => c.Channel).HasConstraintName("FK_NTFY_DAY_CNT_CHNL_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}
