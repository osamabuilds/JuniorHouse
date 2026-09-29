using MediatR;
using Romp.Modules.Quality.Application.Abstractions;

namespace Romp.Modules.Quality.Infrastructure.Persistence;

/// <summary>
/// The QC module's unit of work (same pattern as PROD): runs the handler, then saves exactly once, so the state
/// change, its inbox row and its outbox event commit together (ADR 0004). Applies only to <see cref="IQualityCommand"/> requests.
/// </summary>
public sealed class QcTransactionBehavior<TRequest, TResponse>(QcDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IQualityCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }
}
