using Romp.BuildingBlocks.Domain;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Domain.Channels;

/// <summary>What an inbound WhatsApp event is. Each value has a four-letter code in <c>WBHK_TYP_LKP</c>.</summary>
public enum WhatsAppEventType
{
    ButtonReply,
    Stop,
    Receipt,
}

/// <summary>
/// One inbound WhatsApp event as the webhook received it (CHN-11): the provider's event id (unique, so a replay changes nothing), whether the
/// signature held, and a hash of the body. The body, the sender number and the reply text are not kept (SEC-25). An event whose signature failed is
/// stored with <c>SignatureVerified = false</c> and no type, and is never acted on.
/// </summary>
public sealed class WhatsAppEvent : Entity<long>, IAuditable
{
    private WhatsAppEvent()
    {
        ProviderEventId = string.Empty;
        BodyHash = string.Empty;
        InsrBy = string.Empty;
    }

    public WhatsAppEvent(string providerEventId, WhatsAppEventType? type, bool signatureVerified, string bodyHash, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(providerEventId))
        {
            throw new DomainException("An inbound event needs the provider's event id.");
        }

        ProviderEventId = providerEventId;
        Type = type;
        SignatureVerified = signatureVerified;
        BodyHash = bodyHash;
        ReceivedAt = receivedAt;
        InsrBy = string.Empty;
    }

    public string ProviderEventId { get; private set; }

    public WhatsAppEventType? Type { get; private set; }

    public bool SignatureVerified { get; private set; }

    public string BodyHash { get; private set; }

    /// <summary>The delivery this event answered, when it could be matched by provider message id (CHN-12, CHN-14).</summary>
    public long? MatchedDeliveryId { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public void MarkProcessed(DateTimeOffset at, long? matchedDeliveryId)
    {
        ProcessedAt = at;
        MatchedDeliveryId = matchedDeliveryId;
    }
}

/// <summary>
/// A number that said STOP, kept as a salted hash so the list holds no phone number (CHN-6). Every WhatsApp send checks it.
/// </summary>
public sealed class BlockedNumber : Entity<long>, IAuditable
{
    private BlockedNumber()
    {
        NumberHash = string.Empty;
        InsrBy = string.Empty;
    }

    public BlockedNumber(string numberHash, DateTimeOffset blockedAt)
    {
        if (string.IsNullOrWhiteSpace(numberHash))
        {
            throw new DomainException("A blocked number needs its hash.");
        }

        NumberHash = numberHash;
        BlockedAt = blockedAt;
        InsrBy = string.Empty;
    }

    public string NumberHash { get; private set; }

    public DateTimeOffset BlockedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

public sealed class WhatsAppEventTypeLookup : NotificationsLookup<WhatsAppEventType>;

/// <summary>The four-letter codes of <see cref="WhatsAppEventType"/> in <c>WBHK_TYP_LKP</c>.</summary>
public static class WhatsAppEventCodes
{
    public static string Of(WhatsAppEventType type) => type switch
    {
        WhatsAppEventType.ButtonReply => "BTNR",
        WhatsAppEventType.Stop => "STOP",
        WhatsAppEventType.Receipt => "RCPT",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
