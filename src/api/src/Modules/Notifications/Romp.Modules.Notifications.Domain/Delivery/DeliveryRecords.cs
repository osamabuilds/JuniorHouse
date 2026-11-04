using Romp.BuildingBlocks.Domain;
using Romp.Modules.Notifications.Contracts;

namespace Romp.Modules.Notifications.Domain.Delivery;


/// <summary>What a notification refers to. Only orders for now; later sprints add returns and dispatches as lookup rows, not columns.</summary>
public static class ReferenceKinds
{
    public const string Order = "ORDR";
}

/// <summary>
/// The history of one delivery: one row per attempt (DLV-12). Holds the outcome and an error category only, never a body, address or
/// number (SEC-25).
/// </summary>
public sealed class DeliveryAttempt : Entity<long>, IAuditable
{
    private DeliveryAttempt()
    {
        InsrBy = string.Empty;
    }

    public DeliveryAttempt(long deliveryId, int attemptNumber, DeliveryStatus outcome, ErrorCategory? error, DateTimeOffset at)
    {
        DeliveryId = deliveryId;
        AttemptNumber = attemptNumber;
        Outcome = outcome;
        Error = error;
        AttemptedAt = at;
        InsrBy = string.Empty;
    }

    public long DeliveryId { get; private set; }

    public int AttemptNumber { get; private set; }

    public DeliveryStatus Outcome { get; private set; }

    public ErrorCategory? Error { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// A delivery that ran out of attempts (DLV-3, DLV-12). It keeps references and the attempt count, so a resend has everything it needs;
/// the message is rebuilt from the order at that time.
/// </summary>
public sealed class DeadLetter : AggregateRoot<long>, IAuditable
{
    private DeadLetter()
    {
        InsrBy = string.Empty;
    }

    public DeadLetter(long notificationId, long deliveryId, NotificationKind kind, NotificationChannel channel, ErrorCategory reason, int attemptCount, DateTimeOffset at)
    {
        DeliveryId = deliveryId;
        Kind = kind;
        Channel = channel;
        Reason = reason;
        AttemptCount = attemptCount;
        DeadAt = at;
        InsrBy = string.Empty;

        // Saved with the dead letter, in one transaction, so the alert can never be lost or raised for a delivery that did not die (DLV-3).
        Raise(new NotificationDeadLetteredEvent(notificationId, NotificationCodes.Of(kind), NotificationCodes.Of(channel)));
    }

    public long DeliveryId { get; private set; }

    public NotificationKind Kind { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public ErrorCategory Reason { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset DeadAt { get; private set; }

    public string? ResentBy { get; private set; }

    public DateTimeOffset? ResentAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public void MarkResent(string by, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(by))
        {
            throw new DomainException("A resend records who asked for it.");
        }

        ResentBy = by;
        ResentAt = at;
    }
}

/// <summary>
/// A message template kept as data, versioned (CHN-9). The renderer HTML-encodes every value it puts into it. WhatsApp templates also
/// carry the name Meta approved.
/// </summary>
public sealed class MessageTemplate : Entity<long>, IAuditable
{
    private MessageTemplate()
    {
        Language = "EN";
        Subject = string.Empty;
        PlainText = string.Empty;
        InsrBy = string.Empty;
    }

    public MessageTemplate(NotificationKind kind, NotificationChannel channel, string language, short version, string subject, string plainText, string? html, string? providerTemplateName)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new DomainException("A template needs a plain-text body.");
        }

        Kind = kind;
        Channel = channel;
        Language = language;
        Version = version;
        Subject = subject;
        PlainText = plainText;
        Html = html;
        ProviderTemplateName = providerTemplateName;
        IsActive = true;
        InsrBy = string.Empty;
    }

    public NotificationKind Kind { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public string Language { get; private set; }

    public short Version { get; private set; }

    public string Subject { get; private set; }

    public string PlainText { get; private set; }

    public string? Html { get; private set; }

    /// <summary>The template name Meta approved (WhatsApp only).</summary>
    public string? ProviderTemplateName { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>How many messages went out on one channel on one day, so the cost of WhatsApp is visible from the first day (CHN-15).</summary>
public sealed class DailySendCount : Entity<long>, IAuditable
{
    private DailySendCount()
    {
        InsrBy = string.Empty;
    }

    public DailySendCount(DateOnly day, NotificationChannel channel)
    {
        Day = day;
        Channel = channel;
        InsrBy = string.Empty;
    }

    public DateOnly Day { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public int SentCount { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public void Increment() => SentCount++;
}
