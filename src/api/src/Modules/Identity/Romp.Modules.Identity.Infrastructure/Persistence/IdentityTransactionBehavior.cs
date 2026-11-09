using MediatR;
using Romp.Modules.Identity.Application.Abstractions;

namespace Romp.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Identity's unit of work: runs the handler inside one database transaction, then saves, so the state change, its audit rows and its
/// outbox events commit together (ADR 0004, NFR-FT-08). A command sent from inside another command's handler joins the outer
/// transaction. Applies only to <see cref="IIdentityCommand"/> requests.
/// </summary>
public sealed class IdentityTransactionBehavior<TRequest, TResponse>(IdntDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdentityCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
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
