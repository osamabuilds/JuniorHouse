using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.ProductMedia;

namespace Romp.Modules.Catalog.Infrastructure.Persistence;

/// <summary>
/// MediatR pipeline behaviour (Decorator around the handler) that is the CTLG module's unit of work: runs the handler inside one
/// database transaction, then saves, so the change and the events it raised (the outbox) commit together (ADR 0004, NFR-FT-08). A
/// command sent from inside another command's handler joins the outer transaction. Files queued for removal (a deleted image) are
/// removed only after the commit, and a failed removal is logged, never rolled back. Only applies to requests marked
/// <see cref="ICatalogCommand"/>; queries never reach this behaviour.
/// </summary>
public sealed partial class CatalogTransactionBehavior<TRequest, TResponse>(
    CatalogDbContext dbContext, PendingFileDeletes pendingDeletes, IFileStorage storage, ILogger<CatalogTransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICatalogCommand
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not remove stored file {StorageKey} after commit; it is now an orphan.")]
    private partial void LogOrphan(Exception exception, string storageKey);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            var nested = await next(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return nested;
        }

        TResponse response;
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            response = await next(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        foreach (var key in pendingDeletes.Keys)
        {
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                LogOrphan(exception, key);
            }
        }

        pendingDeletes.Clear();
        return response;
    }
}
