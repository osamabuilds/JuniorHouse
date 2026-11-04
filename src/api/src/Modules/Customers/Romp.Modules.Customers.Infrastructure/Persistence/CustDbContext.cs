using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Domain.Addresses;
using Romp.Modules.Customers.Domain.Newsletter;
using Romp.Modules.Customers.Domain.Wishlist;

namespace Romp.Modules.Customers.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>CUST</c> schema (Customers), including the module's outbox (<c>OUTB_MSG</c>) and inbox (<c>INBX</c>).
/// Implements <see cref="ICustomerDbContext"/> (Application's own port) so handlers never depend on this project (ADR 0002).
/// </summary>
public sealed class CustDbContext(DbContextOptions<CustDbContext> options) : DbContext(options), ICustomerDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerAddress> Addresses => Set<CustomerAddress>();

    public DbSet<WishlistItem> Wishlist => Set<WishlistItem>();

    public DbSet<NewsletterSubscription> Newsletter => Set<NewsletterSubscription>();

    public DbSet<CustomerSession> Sessions => Set<CustomerSession>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();

    async Task ICustomerDbContext.LockCustomerAsync(long customerId, CancellationToken cancellationToken) =>
        await Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"CUST\".\"CUST_MAIN\" WHERE \"ID\" = {customerId} FOR UPDATE", cancellationToken);

    async Task ICustomerDbContext.LockKeyAsync(string key, CancellationToken cancellationToken) =>
        await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);

    void ICustomerDbContext.Discard(object entity) => Entry(entity).State = EntityState.Detached;

    async Task ICustomerDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("CUST");
        modelBuilder.HasOutboxTable("CUST");
        modelBuilder.HasInboxTable("CUST");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
