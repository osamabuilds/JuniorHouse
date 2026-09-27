using MediatR;
using Romp.Modules.Reference.Application;

namespace Romp.Modules.Reference.Infrastructure;

/// <summary>
/// MediatR pipeline behaviour (Decorator around the handler) that is the REF module's unit of
/// work: runs the handler, then calls <c>SaveChangesAsync</c> exactly once, so every write the
/// handler made (including any audit-column stamping and outbox row) commits together in one
/// database transaction (SCRUM-171). Only applies to requests marked <see cref="IReferenceCommand"/> -
/// queries never reach this behaviour.
/// </summary>
public sealed class ReferenceTransactionBehavior<TRequest, TResponse>(ReferenceDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IReferenceCommand
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
