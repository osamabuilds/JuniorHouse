using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Domain.Orders;

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

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Allocates the next order number from the atomic counter (ADR 0006). Runs inside the caller transaction; a rolled-back number is simply skipped.</summary>
public interface IOrderNumberAllocator
{
    Task<string> NextAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
