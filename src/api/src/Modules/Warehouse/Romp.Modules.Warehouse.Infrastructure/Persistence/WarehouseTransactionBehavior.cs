using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Warehouse.Application.Abstractions;

namespace Romp.Modules.Warehouse.Infrastructure.Persistence;

/// <summary>
/// Warehouse's unit of work: runs the handler inside one database transaction, then saves, so the state change, its inbox row and
/// its outbox event commit together (ADR 0004, NFR-FT-08). A command sent from inside another command's handler joins the outer
/// transaction. Applies only to <see cref="IWarehouseCommand"/> requests.
/// </summary>
public sealed class WarehouseTransactionBehavior<TRequest, TResponse>(WhseDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IWarehouseCommand
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
