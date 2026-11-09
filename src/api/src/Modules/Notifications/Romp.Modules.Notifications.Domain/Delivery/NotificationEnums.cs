namespace Romp.Modules.Notifications.Domain.Delivery;

/// <summary>What a notification is about. Each value has a four-letter lookup code in <c>NTFY_KIND_LKP</c>.</summary>
public enum NotificationKind
{
    OrderConfirmation,
    OrderCancelled,
    CodConfirmationRequest,
    OrderDelayed,
}

/// <summary>The way a message travels. Email always; WhatsApp only with opt-in (CHN-1, CHN-2).</summary>
public enum NotificationChannel
{
    Email,
    WhatsApp,
}

/// <summary>Where one channel delivery stands. <c>Sent</c> is final for email; <c>Delivered</c> comes only from a WhatsApp receipt (Q-DLG-2).</summary>
public enum DeliveryStatus
{
    Queued,
    Sent,
    Delivered,
    Failed,
    Dead,
    Expired,
    Skipped,
}

/// <summary>Why an attempt failed or a delivery was skipped. Never carries provider text, an address or a number (SEC-25).</summary>
public enum ErrorCategory
{
    Temporary,
    Permanent,
    CircuitOpen,
    OptedOut,
    NoAddress,
}

/// <summary>The four-letter codes the lookup tables and logs use. Stable: code and database both read them.</summary>
public static class NotificationCodes
{
    public static string Of(NotificationKind kind) => kind switch
    {
        NotificationKind.OrderConfirmation => "ORCF",
        NotificationKind.OrderCancelled => "ORCN",
        NotificationKind.CodConfirmationRequest => "CODR",
        NotificationKind.OrderDelayed => "ORDL",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string Of(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Email => "EMAL",
        NotificationChannel.WhatsApp => "WHTS",
        _ => throw new ArgumentOutOfRangeException(nameof(channel)),
    };

    public static string Of(DeliveryStatus status) => status switch
    {
        DeliveryStatus.Queued => "QUED",
        DeliveryStatus.Sent => "SENT",
        DeliveryStatus.Delivered => "DLVD",
        DeliveryStatus.Failed => "FAIL",
        DeliveryStatus.Dead => "DEAD",
        DeliveryStatus.Expired => "EXPR",
        DeliveryStatus.Skipped => "SKIP",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static string Of(ErrorCategory category) => category switch
    {
        ErrorCategory.Temporary => "TMPY",
        ErrorCategory.Permanent => "PERM",
        ErrorCategory.CircuitOpen => "CIRC",
        ErrorCategory.OptedOut => "OPTO",
        ErrorCategory.NoAddress => "NOAD",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };
}
