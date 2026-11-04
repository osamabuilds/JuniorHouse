using Romp.BuildingBlocks.Domain;
using Romp.Modules.Customers.Domain.Abstractions;

namespace Romp.Modules.Customers.Domain.NotifyMe;

/// <summary>Where a back-in-stock request stands; stored through the NTFM_STS_LKP lookup (ids are stable).</summary>
public enum NotifyStatus : short
{
    Open = 1,
    Notified = 2,
    Expired = 3,
}

/// <summary>The rules about when a restock should notify people and how many may wait. Pure, so they are tested without a database (NTM-3, NTM-4).</summary>
public static class RestockRule
{
    /// <summary>A stock change is a restock when units are available after it.</summary>
    public static bool IsRestock(int available) => available > 0;

    /// <summary>Whether another open request fits under the per-variant cap.</summary>
    public static bool HasRoom(int openCount, int maxPerVariant) => openCount < maxPerVariant;
}

/// <summary>
/// One shopper's wish to be told when one variant is back (FR-11). Rules: one open request per variant and address (the database
/// enforces it); a request is notified exactly once and then closed; an open request that waits too long expires. The address is
/// personal data, so no event or log carries it.
/// </summary>
[Romp.BuildingBlocks.Auditing.NotAudited("Technical: the row holds the address, which must not be copied into an audit trail; status and times on the row are the record.")]
public sealed class NotifyRequest : AggregateRoot<long>, IAuditable
{
    private NotifyRequest()
    {
        Email = string.Empty;
        InsrBy = string.Empty;
    }

    /// <summary>The Labelling variant id, as the stock events and the storefront read model carry it.</summary>
    public long VariantId { get; private set; }

    /// <summary>The normalised address (lower case, trimmed).</summary>
    public string Email { get; private set; }

    public NotifyStatus Status { get; private set; }

    public DateTimeOffset? NotifiedAt { get; private set; }

    /// <summary>When the restock email went out; empty until it does. A Notified request with no send time is mail still to retry.</summary>
    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Starts an open request. <paramref name="email"/> must already be normalised with <see cref="NotifyEmail.Normalize"/>.</summary>
    public static NotifyRequest Open(long variantId, string email, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(variantId);
        if (NotifyEmail.Normalize(email) != email)
        {
            throw new ArgumentException("The email must be normalised first.", nameof(email));
        }

        _ = now;
        return new NotifyRequest { VariantId = variantId, Email = email, Status = NotifyStatus.Open };
    }

    /// <summary>Closes an open request because the variant is back. Returns false when it was not open, so a request is notified exactly once.</summary>
    public bool MarkNotified(DateTimeOffset now)
    {
        if (Status != NotifyStatus.Open)
        {
            return false;
        }

        Status = NotifyStatus.Notified;
        NotifiedAt = now;
        return true;
    }

    /// <summary>Closes an open request that waited past the retention period. Returns false when it was not open.</summary>
    public bool Expire(DateTimeOffset now)
    {
        _ = now;
        if (Status != NotifyStatus.Open)
        {
            return false;
        }

        Status = NotifyStatus.Expired;
        return true;
    }

    /// <summary>Records that the email went out. Only a notified request that has not been sent yet takes it; returns whether it did.</summary>
    public bool MarkSent(DateTimeOffset now)
    {
        if (Status != NotifyStatus.Notified || SentAt is not null)
        {
            return false;
        }

        SentAt = now;
        return true;
    }

    /// <summary>Queues the restock email through the outbox (ADR 0022). The event names the record by id only; call it after the record has an id.</summary>
    public void QueueRestockNotice()
    {
        if (Id <= 0)
        {
            throw new InvalidOperationException("The request must be saved before its restock email can be queued.");
        }

        Raise(new CustomerEmailRequestedEvent(CustomerEmailKind.RestockNotice, Id));
    }
}
