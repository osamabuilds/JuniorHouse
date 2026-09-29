using MediatR;
using Romp.Modules.Catalog.Application;
using Romp.Modules.Catalog.Application.Abstractions;

namespace Romp.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// MediatR pipeline behaviour (Decorator around the handler) that is the CTLG module's unit of
/// work: runs the handler, then calls <c>SaveChangesAsync</c> exactly once, so every write the
/// handler made commits together in one database transaction (SCRUM-171). Only applies to
/// requests marked <see cref="ICatalogCommand"/> - queries never reach this behaviour.
/// </summary>
public sealed class CatalogTransactionBehavior<TRequest, TResponse>(CatalogDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICatalogCommand
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }
}
