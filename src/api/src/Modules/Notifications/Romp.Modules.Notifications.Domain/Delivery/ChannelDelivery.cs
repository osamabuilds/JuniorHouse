using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Notifications.Domain.Delivery;

/// <summary>
/// One notification travelling over one channel (ADR 0026). The state machine lives here:
/// <c>Queued → Sent → Delivered</c>; <c>Queued → Queued</c> on a temporary failure, until the attempts run out (<c>Dead</c>);
/// <c>Queued → Failed</c> on a permanent one (DLV-8); <c>Queued → Expired</c> when it is too late to be useful (DLV-10);
/// <c>Queued → Skipped</c> when the customer cannot be reached on this channel (CHN-2). Nothing else is allowed.
/// </summary>
public sealed class ChannelDelivery : Entity<long>, IAuditable
{
    private ChannelDelivery()
    {
        InsrBy = string.Empty;
    }

    public ChannelDelivery(NotificationChannel channel, DateTimeOffset now, long? fallbackOfId = null, string? resendKey = null)
    {
        ResendKey = resendKey;
        Channel = channel;
        Status = DeliveryStatus.Queued;
        NextAttemptAt = now;
        FallbackOfId = fallbackOfId;
        InsrBy = string.Empty;
    }

    public long NotificationId { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public DeliveryStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    /// <summary>Set while a worker holds the delivery; a crashed worker's lease expires and the next claim picks it up.</summary>
    public DateTimeOffset? LeaseUntil { get; private set; }

    public ErrorCategory? LastError { get; private set; }

    public string? ProviderMessageId { get; private set; }

    /// <summary>Set on a resend or a fallback: the delivery this one replaces.</summary>
    public long? FallbackOfId { get; private set; }

    /// <summary>The client key of the staff resend that created this delivery; the same key twice creates nothing new (DLG-8).</summary>
    public string? ResendKey { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public bool IsFinal => Status is not DeliveryStatus.Queued;

    public bool IsDue(DateTimeOffset now) =>
        Status == DeliveryStatus.Queued && NextAttemptAt <= now && (LeaseUntil is null || LeaseUntil <= now);

    /// <summary>A worker takes the delivery for <paramref name="lease"/>; two workers never hold it at once (DLV-9).</summary>
    public bool TryClaim(DateTimeOffset now, TimeSpan lease)
    {
        if (!IsDue(now))
        {
            return false;
        }

        LeaseUntil = now + lease;
        return true;
    }

    /// <summary>The provider took the message. Counts as an attempt.</summary>
    public void MarkSent(string? providerMessageId)
    {
        RequireQueued();
        AttemptCount++;
        Status = DeliveryStatus.Sent;
        ProviderMessageId = providerMessageId;
        NextAttemptAt = null;
        LeaseUntil = null;
        LastError = null;
    }

    /// <summary>A receipt says the phone has it (WhatsApp only). Repeating it changes nothing.</summary>
    public void MarkDelivered()
    {
        if (Status == DeliveryStatus.Delivered)
        {
            return;
        }

        if (Status != DeliveryStatus.Sent)
        {
            throw new DomainException("Only a sent message can be delivered.");
        }

        Status = DeliveryStatus.Delivered;
    }

    /// <summary>The provider reports it could not deliver a message it had accepted (a WhatsApp "failed" receipt). Final; the email, sent in parallel, stands (CHN-3).</summary>
    public void MarkFailedByProvider()
    {
        if (Status == DeliveryStatus.Failed)
        {
            return;
        }

        if (Status != DeliveryStatus.Sent)
        {
            throw new DomainException("Only a sent message can be reported failed by the provider.");
        }

        Status = DeliveryStatus.Failed;
        LastError = ErrorCategory.Permanent;
    }

    /// <summary>
    /// An attempt failed. A permanent failure is final and not retried (DLV-8); a temporary one is retried on the schedule until the
    /// attempts run out, then the delivery is dead (DLV-3). An open circuit waits without using up an attempt (CHN-4).
    /// </summary>
    public void RecordFailure(ErrorCategory category, DateTimeOffset now, RetryPolicy policy, double jitterSample)
    {
        RequireQueued();
        LastError = category;
        LeaseUntil = null;

        switch (category)
        {
            case ErrorCategory.Permanent:
            case ErrorCategory.NoAddress:
                AttemptCount++;
                Status = DeliveryStatus.Failed;
                NextAttemptAt = null;
                break;
            case ErrorCategory.CircuitOpen:
                NextAttemptAt = now + policy.FirstDelay;
                break;
            case ErrorCategory.Temporary:
                AttemptCount++;
                if (AttemptCount >= policy.MaxAttempts)
                {
                    Status = DeliveryStatus.Dead;
                    NextAttemptAt = null;
                }
                else
                {
                    NextAttemptAt = now + policy.DelayAfter(AttemptCount, jitterSample);
                }

                break;
            default:
                throw new DomainException("A skip is not a failure; use Skip.");
        }
    }

    /// <summary>It is too late for this message to help the customer (DLV-10).</summary>
    public void Expire()
    {
        RequireQueued();
        Status = DeliveryStatus.Expired;
        NextAttemptAt = null;
        LeaseUntil = null;
    }

    /// <summary>This channel is not used for this customer: no opt-in, number suppressed, nowhere to send (CHN-2, CHN-6).</summary>
    public void Skip(ErrorCategory reason)
    {
        RequireQueued();
        if (reason is not (ErrorCategory.OptedOut or ErrorCategory.NoAddress))
        {
            throw new DomainException("A delivery is skipped only for a missing opt-in, a suppression or a missing address.");
        }

        Status = DeliveryStatus.Skipped;
        LastError = reason;
        NextAttemptAt = null;
        LeaseUntil = null;
    }

    private void RequireQueued()
    {
        if (Status != DeliveryStatus.Queued)
        {
            throw new DomainException($"A {Status} delivery cannot change this way.");
        }
    }
}
