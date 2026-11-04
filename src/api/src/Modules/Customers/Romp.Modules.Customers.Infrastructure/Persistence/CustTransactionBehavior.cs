using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Abstractions;

namespace Romp.Modules.Customers.Infrastructure.Persistence;

/// <summary>
/// The Customers unit of work: runs the handler inside one database transaction, then saves, so the state change, its inbox row and its
/// outbox event commit together (ADR 0004, NFR-FT-08). Applies only to <see cref="ICustomerCommand"/> requests.
/// </summary>
public sealed class CustTransactionBehavior<TRequest, TResponse>(CustDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICustomerCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // A command sent from inside another command's handler joins the outer transaction.
        if (dbContext.Database.CurrentTransaction is not null)
        {
            var nested = await next(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return nested;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var response = await next(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
