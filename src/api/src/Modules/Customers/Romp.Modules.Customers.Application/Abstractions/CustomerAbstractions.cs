using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Domain.Addresses;
using Romp.Modules.Customers.Domain.Newsletter;
using Romp.Modules.Customers.Domain.NotifyMe;
using Romp.Modules.Customers.Domain.Wishlist;

namespace Romp.Modules.Customers.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Customers so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface ICustomerCommand;

/// <summary>Customers persistence port (Application owns it, Infrastructure's CustDbContext implements it; ADR 0002). Feature sets are added with their features.</summary>
public interface ICustomerDbContext
{
    DbSet<Customer> Customers { get; }

    DbSet<CustomerAddress> Addresses { get; }

    DbSet<WishlistItem> Wishlist { get; }

    DbSet<NewsletterSubscription> Newsletter { get; }

    DbSet<NotifyRequest> NotifyRequests { get; }

    DbSet<CustomerSession> Sessions { get; }

    DbSet<LoginAttempt> LoginAttempts { get; }

    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    DbSet<EmailVerificationToken> EmailVerificationTokens { get; }

    /// <summary>Forgets a tracked entity after a failed save, so the change is dropped and not retried by a later save.</summary>
    void Discard(object entity);

    /// <summary>Takes a row lock on the customer until the transaction ends, so changes to that customer's owned lists run one at a time.</summary>
    Task LockCustomerAsync(long customerId, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a transaction-scoped lock on a name (an email address, say) so two requests about the same value run one at a time even when
    /// no row exists yet. Released when the transaction ends.
    /// </summary>
    Task LockKeyAsync(string key, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The only way a feature learns who the customer is (ADR 0021, SEC-05). It exposes the id and nothing else, and no endpoint or handler
/// may take a customer id from a route, query string or body. Until real sign-in exists the implementation is a development-only
/// stand-in.
/// </summary>
public interface ICurrentCustomer : Contracts.ICurrentCustomerAccessor
{
    bool IsSignedIn => CustomerId is not null;

    /// <summary>The signed-in customer id, or an <see cref="UnauthorizedAccessException"/> for a guest (the web layer answers 401).</summary>
    long Require() => CustomerId ?? throw new UnauthorizedAccessException("A signed-in customer is required.");
}
