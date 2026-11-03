using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Notifications.Domain.Delivery;

/// <summary>
/// One message the business owes a customer, created once per business event (ADR 0026, DLV-4): unique on the event id and kind, so a
/// redelivered event creates nothing new. It holds ids only: no body, address or phone (SEC-25); those are read from Orders at send time.
/// </summary>
public sealed class Notification : Entity<long>, IAuditable
{
    private readonly List<ChannelDelivery> _deliveries = [];

    private Notification()
    {
        ReferenceKind = string.Empty;
        InsrBy = string.Empty;
    }

    public Notification(NotificationKind kind, string referenceKind, long referenceId, Guid eventId, DateTimeOffset now, TimeSpan validFor, int priority)
    {
        if (string.IsNullOrWhiteSpace(referenceKind))
        {
            throw new DomainException("A notification needs the kind of thing it is about.");
        }

        if (validFor <= TimeSpan.Zero)
        {
            throw new DomainException("A notification must be useful for some time.");
        }

        Kind = kind;
        ReferenceKind = referenceKind;
        ReferenceId = referenceId;
        EventId = eventId;
        ExpiresAt = now + validFor;
        Priority = priority;
        InsrBy = string.Empty;
    }

    public NotificationKind Kind { get; private set; }

    /// <summary>The kind of thing this is about; <c>ORDR</c> for an order.</summary>
    public string ReferenceKind { get; private set; }

    public long ReferenceId { get; private set; }

    public Guid EventId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Lower is sooner. Confirmations before reminders (DLV-11).</summary>
    public int Priority { get; private set; }

    public IReadOnlyCollection<ChannelDelivery> Deliveries => _deliveries;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>Adds the first delivery for a channel; a notification has at most one original per channel (DLV-4).</summary>
    public ChannelDelivery AddDelivery(NotificationChannel channel, DateTimeOffset now)
    {
        if (_deliveries.Any(d => d.Channel == channel && d.FallbackOfId is null))
        {
            throw new DomainException("This notification already has a delivery on that channel.");
        }

        var delivery = new ChannelDelivery(channel, now);
        _deliveries.Add(delivery);
        return delivery;
    }

    /// <summary>
    /// Staff send one delivery again (FR-63, DLG-2): a new queued delivery linked to the one it replaces, which stays as history. A notification
    /// that has expired is given a fresh life, because a person has decided the customer still needs it.
    /// </summary>
    public ChannelDelivery AddResend(ChannelDelivery original, string resendKey, DateTimeOffset now, TimeSpan validFor)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (string.IsNullOrWhiteSpace(resendKey))
        {
            throw new DomainException("A resend needs a key so a double click does not send twice.");
        }

        if (original.Id == 0 || _deliveries.All(d => d.Id != original.Id))
        {
            throw new DomainException("A resend replaces a saved delivery of this notification.");
        }

        if (original.Status is DeliveryStatus.Queued or DeliveryStatus.Skipped)
        {
            throw new DomainException($"A {original.Status} delivery cannot be resent.");
        }

        if (now + validFor > ExpiresAt)
        {
            ExpiresAt = now + validFor;
        }

        var delivery = new ChannelDelivery(original.Channel, now, original.Id, resendKey);
        _deliveries.Add(delivery);
        return delivery;
    }

    /// <summary>Creates the deliveries the channel plan asks for. A skipped WhatsApp delivery is recorded so the log says why none was sent.</summary>
    public void Plan(ChannelPlanResult plan, DateTimeOffset now)
    {
        if (plan.Email)
        {
            AddDelivery(NotificationChannel.Email, now);
        }

        if (plan.WhatsApp)
        {
            AddDelivery(NotificationChannel.WhatsApp, now);
        }
        else if (plan.WhatsAppSkipReason is { } reason)
        {
            AddDelivery(NotificationChannel.WhatsApp, now).Skip(reason);
        }
    }
}
