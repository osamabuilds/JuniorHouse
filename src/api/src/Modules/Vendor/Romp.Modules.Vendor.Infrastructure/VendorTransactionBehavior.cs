using MediatR;
using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Infrastructure;

/// <summary>
/// MediatR pipeline behaviour (Decorator around the handler) that is the VNDR module's unit of
/// work: runs the handler, then calls <c>SaveChangesAsync</c> exactly once, so every write the
/// handler made (including a PO status transition's history row and outbox event) commits
/// together in one database transaction (SCRUM-171, ADR 0004). Only applies to requests marked
/// <see cref="IVendorCommand"/> - queries never reach this behaviour.
/// </summary>
public sealed class VendorTransactionBehavior<TRequest, TResponse>(VendorDbContext dbContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IVendorCommand
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
