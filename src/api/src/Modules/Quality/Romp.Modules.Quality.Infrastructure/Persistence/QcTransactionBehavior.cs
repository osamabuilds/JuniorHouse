using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Application.Abstractions;

namespace Romp.Modules.Quality.Infrastructure.Persistence;

/// <summary>
/// The QC module's unit of work: runs the handler inside one database transaction, then saves, so the state change, its
/// inbox row and its outbox event commit together (ADR 0004, NFR-FT-08). A handler may call SaveChanges more than once
/// (for example to learn a new row's id before raising the event that carries it); the transaction keeps those steps atomic.
/// Applies only to <see cref="IQualityCommand"/> requests.
/// </summary>
public sealed class QcTransactionBehavior<TRequest, TResponse>(QcDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQualityCommand
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
