using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Notifications.Domain.Channels;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Delivery;

namespace Romp.Modules.Notifications.Infrastructure.Channels;

internal static class ChannelConversions
{
    public static short EventTypeId(WhatsAppEventType type) => (short)((int)type + 1);
}

internal sealed class WhatsAppEventConfiguration : IEntityTypeConfiguration<WhatsAppEvent>
{
    public void Configure(EntityTypeBuilder<WhatsAppEvent> builder)
    {
        builder.ToTable("NTFY_WBHK");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(e => e.ProviderEventId).HasColumnName("PRVD_EVNT_ID").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Type).HasColumnName("WBHK_TYP_ID")
            .HasConversion(v => v == null ? (short?)null : ChannelConversions.EventTypeId(v.Value), v => v == null ? null : (WhatsAppEventType?)(v.Value - 1));
        builder.Property(e => e.SignatureVerified).HasColumnName("SGNT_VRFY_IND").IsRequired();
        builder.Property(e => e.BodyHash).HasColumnName("BODY_HASH").HasMaxLength(64).IsRequired();
        builder.Property(e => e.MatchedDeliveryId).HasColumnName("NTFY_CHNL_ID");
        builder.Property(e => e.ReceivedAt).HasColumnName("RCVD_DTE").IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("PROC_DTE");
        builder.HasAuditColumns();

        builder.HasIndex(e => e.ProviderEventId).IsUnique().HasDatabaseName("UQ_NTFY_WBHK_PRVD_EVNT_ID");
        builder.HasIndex(e => e.Type).HasDatabaseName("IX_NTFY_WBHK_WBHK_TYP_ID");
        builder.HasIndex(e => e.MatchedDeliveryId).HasDatabaseName("IX_NTFY_WBHK_NTFY_CHNL_ID");

        builder.HasOne<WhatsAppEventTypeLookup>().WithMany().HasForeignKey(e => e.Type).HasConstraintName("FK_NTFY_WBHK_WBHK_TYP_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChannelDelivery>().WithMany().HasForeignKey(e => e.MatchedDeliveryId).HasConstraintName("FK_NTFY_WBHK_NTFY_CHNL").OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class BlockedNumberConfiguration : IEntityTypeConfiguration<BlockedNumber>
{
    public void Configure(EntityTypeBuilder<BlockedNumber> builder)
    {
        builder.ToTable("NTFY_BLCK");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(b => b.NumberHash).HasColumnName("NO_HASH").HasMaxLength(64).IsRequired();
        builder.Property(b => b.BlockedAt).HasColumnName("BLCK_DTE").IsRequired();
        builder.HasAuditColumns();

        builder.HasIndex(b => b.NumberHash).IsUnique().HasDatabaseName("UQ_NTFY_BLCK_NO_HASH");
    }
}
