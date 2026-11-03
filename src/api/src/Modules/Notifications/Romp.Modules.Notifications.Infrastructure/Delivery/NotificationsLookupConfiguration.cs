using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Romp.Modules.Notifications.Domain.Channels;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Infrastructure.Delivery;

/// <summary>
/// The system-owned Notifications lookups (docs/db/naming.md standard lookup shape), seeded by migration. The codes are the stable keys that
/// code, logs and specs refer to. Ids are the enum values plus one (see <see cref="DeliveryConversions"/>) and are never reused.
/// </summary>
internal static class NotificationsLookupConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        Configure<NotificationKindLookup, NotificationKind>(modelBuilder, "NTFY_KIND_LKP", DeliveryConversions.KindId, k => (NotificationKind)(k - 1), NotificationCodes.Of,
            (NotificationKind.OrderConfirmation, "Order confirmation", "The order was received (cash on delivery) or paid (online)"),
            (NotificationKind.OrderCancelled, "Order cancelled", "The order was cancelled; says why in a category"),
            (NotificationKind.CodConfirmationRequest, "Cash on delivery confirmation request", "Asks the customer to confirm or cancel a cash on delivery order"));

        Configure<ChannelLookup, NotificationChannel>(modelBuilder, "CHNL_LKP", DeliveryConversions.ChannelId, k => (NotificationChannel)(k - 1), NotificationCodes.Of,
            (NotificationChannel.Email, "Email", "Email through the SMTP sender"),
            (NotificationChannel.WhatsApp, "WhatsApp", "WhatsApp, only for customers who opted in"));

        Configure<DeliveryStatusLookup, DeliveryStatus>(modelBuilder, "CHNL_STS_LKP", DeliveryConversions.StatusId, k => (DeliveryStatus)(k - 1), NotificationCodes.Of,
            (DeliveryStatus.Queued, "Queued", "Waiting to be sent or to be retried"),
            (DeliveryStatus.Sent, "Sent", "Handed to the provider"),
            (DeliveryStatus.Delivered, "Delivered", "The provider confirmed it reached the customer"),
            (DeliveryStatus.Failed, "Failed", "A permanent error; not retried"),
            (DeliveryStatus.Dead, "Dead", "Every attempt failed; in the dead-letter table"),
            (DeliveryStatus.Expired, "Expired", "Too late to be useful, so it was not sent"),
            (DeliveryStatus.Skipped, "Skipped", "Not sent on this channel: no opt-in, stopped or no address"));

        Configure<ErrorCategoryLookup, ErrorCategory>(modelBuilder, "ERR_CATG_LKP", DeliveryConversions.ErrorId, k => (ErrorCategory)(k - 1), NotificationCodes.Of,
            (ErrorCategory.Temporary, "Temporary", "The provider or network failed; worth retrying"),
            (ErrorCategory.Permanent, "Permanent", "The address or number is invalid; retrying will not help"),
            (ErrorCategory.CircuitOpen, "Circuit open", "The channel is paused after repeated failures"),
            (ErrorCategory.OptedOut, "Opted out", "No opt-in, or the number said STOP"),
            (ErrorCategory.NoAddress, "No address", "There is no email address or number to send to"));

        Configure<ReferenceKindLookup, string>(modelBuilder, "REF_KIND_LKP", DeliveryConversions.ReferenceKindId, DeliveryConversions.ReferenceKindCode, code => code,
            (ReferenceKinds.Order, "Order", "The notification is about an order"));

        Configure<WhatsAppEventTypeLookup, WhatsAppEventType>(modelBuilder, "WBHK_TYP_LKP", Channels.ChannelConversions.EventTypeId, k => (WhatsAppEventType)(k - 1), WhatsAppEventCodes.Of,
            (WhatsAppEventType.ButtonReply, "Button reply", "The customer pressed a quick-reply button (Confirm or Cancel)"),
            (WhatsAppEventType.Stop, "Stop", "The customer asked to stop WhatsApp messages"),
            (WhatsAppEventType.Receipt, "Receipt", "A delivery receipt from the provider"));
    }

    private static void Configure<TLookup, TKey>(
        ModelBuilder modelBuilder,
        string tableName,
        Func<TKey, short> toId,
        Func<short, TKey> fromId,
        Func<TKey, string> toCode,
        params (TKey Key, string Name, string Description)[] rows)
        where TLookup : NotificationsLookup<TKey>
        where TKey : notnull
    {
        var builder = modelBuilder.Entity<TLookup>();
        builder.ToTable(tableName);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedNever()
            .HasConversion(new ValueConverter<TKey, short>(k => toId(k), i => fromId(i)));
        builder.Property(l => l.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(l => l.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(l => l.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasData(rows.Select(r => new
        {
            Id = r.Key, Code = toCode(r.Key), r.Name, Description = (string?)r.Description, SortSeq = toId(r.Key), IsActive = true,
        }).Cast<object>().ToArray());
    }
}
