using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;

namespace Romp.Modules.Orders.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Orders so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface IOrdersCommand;

/// <summary>Orders persistence port (Application owns it, Infrastructure OrdrDbContext implements it; ADR 0002).</summary>
public interface IOrdersDbContext
{
    DbSet<Order> Orders { get; }

    DbSet<OrderLine> OrderLines { get; }

    DbSet<OrderHistory> OrderHistory { get; }

    DbSet<CodRisk> CodRisks { get; }

    DbSet<LookupAttempt> LookupAttempts { get; }

    DbSet<Payment> Payments { get; }

    DbSet<PaymentAttempt> PaymentAttempts { get; }

    DbSet<PaymentWebhook> PaymentWebhooks { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Takes the database row lock on one payment (<c>SELECT ... FOR UPDATE</c>) inside the current transaction, so two messages about the same
    /// payment (a webhook and the reconciliation job, or a repeated delivery) are handled one after the other and the money counts once.
    /// Waits for the other to commit. Must be called inside the Orders transaction.
    /// </summary>
    Task LockPaymentAsync(long paymentId, CancellationToken cancellationToken);

    /// <summary>Forgets every tracked change. Used after a failed unit of work so a later one in the same scope does not re-save it.</summary>
    void DiscardChanges();
}

/// <summary>Allocates the next order number from the atomic counter (ADR 0006). Runs inside the caller transaction; a rolled-back number is simply skipped.</summary>
public interface IOrderNumberAllocator
{
    Task<string> NextAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
