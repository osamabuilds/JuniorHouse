using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Customers so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface ICustomerCommand;

/// <summary>Customers persistence port (Application owns it, Infrastructure's CustDbContext implements it; ADR 0002). Feature sets are added with their features.</summary>
public interface ICustomerDbContext
{
    DbSet<Customer> Customers { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The only way a feature learns who the customer is (ADR 0021, SEC-05). It exposes the id and nothing else, and no endpoint or handler
/// may take a customer id from a route, query string or body. Until real sign-in exists the implementation is a development-only
/// stand-in.
/// </summary>
public interface ICurrentCustomer
{
    long? CustomerId { get; }

    bool IsSignedIn => CustomerId is not null;

    /// <summary>The signed-in customer id, or an <see cref="UnauthorizedAccessException"/> for a guest (the web layer answers 401).</summary>
    long Require() => CustomerId ?? throw new UnauthorizedAccessException("A signed-in customer is required.");
}
