using MediatR;
using Romp.Modules.Production.Application.Abstractions;

namespace Romp.Modules.Production.Infrastructure.Persistence;

/// <summary>
/// The PROD module's unit of work (same pattern as VNDR): runs the handler, then saves exactly once,
/// so the state change, its inbox row and its outbox event commit together (ADR 0004). Applies only
/// to <see cref="IProductionCommand"/> requests.
/// </summary>
public sealed class ProductionTransactionBehavior<TRequest, TResponse>(ProductionDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IProductionCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }
}
