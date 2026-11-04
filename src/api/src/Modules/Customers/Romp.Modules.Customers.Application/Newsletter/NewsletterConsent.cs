using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Newsletter;

namespace Romp.Modules.Customers.Application.Newsletter;

/// <summary>
/// The one place consent changes are made (FR-41, NLS-1, NLS-4, NLS-5), so a subscribe from the footer, the account page or the
/// registration form behaves identically. Callers run inside the Customers transaction (an <see cref="ICustomerCommand"/>).
/// Two requests for one address take a transaction-scoped lock on it, so they run one at a time and the unique index is only a backstop.
/// </summary>
public sealed class NewsletterConsent(ICustomerDbContext dbContext, TimeProvider clock)
{
    /// <summary>
    /// Subscribes an address on the person's explicit action. A new address, or one that had unsubscribed, becomes subscribed and gets one
    /// welcome email queued; an address already subscribed is left alone (no history, no second welcome).
    /// </summary>
    /// <returns>True when this was a new consent, false when nothing changed.</returns>
    public async Task<bool> SubscribeAsync(string normalisedEmail, ConsentSource source, CancellationToken cancellationToken)
    {
        await dbContext.LockKeyAsync(LockName(normalisedEmail), cancellationToken);
        var now = clock.GetUtcNow();
        var subscription = await dbContext.Newsletter.FirstOrDefaultAsync(s => s.Email == normalisedEmail, cancellationToken);

        if (subscription is null)
        {
            subscription = NewsletterSubscription.Start(normalisedEmail, source, now);
            dbContext.Newsletter.Add(subscription);
            await dbContext.SaveChangesAsync(cancellationToken); // the welcome event names the record by id
        }
        else if (!subscription.Subscribe(source, now))
        {
            return false;
        }

        subscription.QueueWelcome();
        return true;
    }

    /// <summary>Withdraws consent for the record a signed link names. An unknown or already-unsubscribed record changes nothing.</summary>
    public async Task UnsubscribeByIdAsync(long subscriptionId, ConsentSource source, CancellationToken cancellationToken)
    {
        var subscription = await LoadLockedAsync(subscriptionId, cancellationToken);
        subscription?.Unsubscribe(source, clock.GetUtcNow());
    }

    /// <summary>Withdraws consent for an address (the signed-in customer turning it off).</summary>
    public async Task UnsubscribeAsync(string normalisedEmail, ConsentSource source, CancellationToken cancellationToken)
    {
        await dbContext.LockKeyAsync(LockName(normalisedEmail), cancellationToken);
        var subscription = await dbContext.Newsletter.FirstOrDefaultAsync(s => s.Email == normalisedEmail, cancellationToken);
        subscription?.Unsubscribe(source, clock.GetUtcNow());
    }

    /// <summary>
    /// Loads a record by id after taking the lock on its address. The address is read first without tracking, then the lock is taken, then the
    /// record is read for update, so a send or a change that held the lock has finished before this one reads (no row-version conflict).
    /// </summary>
    public async Task<NewsletterSubscription?> LoadLockedAsync(long subscriptionId, CancellationToken cancellationToken)
    {
        var email = await dbContext.Newsletter.AsNoTracking().Where(s => s.Id == subscriptionId).Select(s => s.Email).FirstOrDefaultAsync(cancellationToken);
        if (email is null)
        {
            return null;
        }

        await dbContext.LockKeyAsync(LockName(email), cancellationToken);
        return await dbContext.Newsletter.FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);
    }

    private static string LockName(string email) => "newsletter:" + email;
}
