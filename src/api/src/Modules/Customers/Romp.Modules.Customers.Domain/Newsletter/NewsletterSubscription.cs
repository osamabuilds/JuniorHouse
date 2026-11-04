using Romp.BuildingBlocks.Domain;
using Romp.Modules.Customers.Domain.Abstractions;

namespace Romp.Modules.Customers.Domain.Newsletter;

/// <summary>Whether an address currently receives marketing email; stored through the NWSL_STS_LKP lookup (ids are stable).</summary>
public enum NewsletterStatus : short
{
    Subscribed = 1,
    Unsubscribed = 2,
}

/// <summary>What a consent history row records; stored through the NWSL_ACTN_LKP lookup.</summary>
public enum ConsentAction : short
{
    Subscribed = 1,
    Unsubscribed = 2,
}

/// <summary>Where consent was given or withdrawn (NLS-5); stored through the NWSL_SRC_LKP lookup.</summary>
public enum ConsentSource : short
{
    Registration = 1,
    Account = 2,
    Footer = 3,
    Link = 4,
}

/// <summary>
/// One row of the append-only consent history (NLS-5): what was done, from where and when. Rows are never updated or deleted.
/// </summary>
[Romp.BuildingBlocks.Auditing.NotAudited("Technical: this table is itself the consent record; rows are append-only.")]
public sealed class ConsentEvent : Entity<long>, IAuditable
{
    private ConsentEvent()
    {
        InsrBy = string.Empty;
    }

    public long SubscriptionId { get; private set; }

    public ConsentAction Action { get; private set; }

    public ConsentSource Source { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    internal static ConsentEvent Of(ConsentAction action, ConsentSource source, DateTimeOffset at) =>
        new() { Action = action, Source = source, OccurredAt = at };
}

/// <summary>
/// The marketing-email consent of one address (FR-41). There is one record per normalised email. Rules:
/// <list type="bullet">
/// <item>subscribing twice changes nothing and is not a new consent (NLS-1);</item>
/// <item>an unsubscribe holds until the person subscribes again on purpose; nothing else re-subscribes the address (NLS-4);</item>
/// <item>every change adds a history row with its source and time (NLS-5).</item>
/// </list>
/// </summary>
[Romp.BuildingBlocks.Auditing.NotAudited("Technical: the consent history (NWSL_HIST) is the audit record, and the address is personal data that must not be copied elsewhere.")]
public sealed class NewsletterSubscription : AggregateRoot<long>, IAuditable
{
    private readonly List<ConsentEvent> _history = [];

    private NewsletterSubscription()
    {
        Email = string.Empty;
        InsrBy = string.Empty;
    }

    /// <summary>The normalised address (lower case, trimmed).</summary>
    public string Email { get; private set; }

    public NewsletterStatus Status { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>When the welcome email for the current subscription went out; empty until it does (one welcome per subscription).</summary>
    public DateTimeOffset? WelcomeSentAt { get; private set; }

    public IReadOnlyList<ConsentEvent> History => _history;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public bool IsSubscribed => Status == NewsletterStatus.Subscribed;

    /// <summary>Creates a new subscribed record. <paramref name="email"/> must already be normalised with <see cref="NewsletterEmail.Normalize"/>.</summary>
    public static NewsletterSubscription Start(string email, ConsentSource source, DateTimeOffset now)
    {
        if (NewsletterEmail.Normalize(email) != email)
        {
            throw new ArgumentException("The email must be normalised first.", nameof(email));
        }

        var subscription = new NewsletterSubscription
        {
            Email = email,
            Status = NewsletterStatus.Subscribed,
            ChangedAt = now,
        };
        subscription._history.Add(ConsentEvent.Of(ConsentAction.Subscribed, source, now));
        return subscription;
    }

    /// <summary>
    /// An explicit subscribe. Returns true when this is a new consent (the address was not subscribed), false when nothing changed.
    /// An already-subscribed record is never touched, so a repeat adds no history.
    /// </summary>
    public bool Subscribe(ConsentSource source, DateTimeOffset now)
    {
        if (IsSubscribed)
        {
            return false;
        }

        Status = NewsletterStatus.Subscribed;
        ChangedAt = now;
        WelcomeSentAt = null;
        _history.Add(ConsentEvent.Of(ConsentAction.Subscribed, source, now));
        return true;
    }

    /// <summary>Withdraws consent. Returns true when the address was subscribed, false when it already was not (idempotent).</summary>
    public bool Unsubscribe(ConsentSource source, DateTimeOffset now)
    {
        if (!IsSubscribed)
        {
            return false;
        }

        Status = NewsletterStatus.Unsubscribed;
        ChangedAt = now;
        _history.Add(ConsentEvent.Of(ConsentAction.Unsubscribed, source, now));
        return true;
    }

    /// <summary>Records that the welcome went out. Needs a saved record (the event names it by id, never by address).</summary>
    public void MarkWelcomeSent(DateTimeOffset now) => WelcomeSentAt = now;

    /// <summary>
    /// Queues the welcome email through the outbox (ADR 0022). The event carries the record's id only. Call it after the record has been
    /// saved once, so it has an id.
    /// </summary>
    public void QueueWelcome()
    {
        if (Id <= 0)
        {
            throw new InvalidOperationException("The subscription must be saved before its welcome email can be queued.");
        }

        Raise(new CustomerEmailRequestedEvent(CustomerEmailKind.NewsletterWelcome, Id));
    }
}
