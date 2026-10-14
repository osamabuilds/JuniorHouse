using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Production.Application.Abstractions;

namespace Romp.Modules.Production.Application.ProductionRuns;

/// <summary>AC-29: the PO was cancelled, so its run closes as cancelled. No run = nothing to do; already cancelled/closed = no-op.</summary>
public sealed record MarkRunCancelledCommand(long PoId) : IRequest, IProductionCommand;

/// <summary>D2: the PO was closed manually; its run closes with it.</summary>
public sealed record MarkRunClosedCommand(long PoId) : IRequest, IProductionCommand;

/// <summary>AC-16 / AC-27: a PO revision came into force. Refreshes the run's latest-acceptable-date copy and, after a PP approval, supersedes the approval (recommending a re-sample for a construction change).</summary>
public sealed record RefreshRunTermsCommand(long PoId, DateOnly? LatestAcceptableDate, bool IsConstructionChange) : IRequest, IProductionCommand;

public sealed class MarkRunCancelledCommandHandler(IProductionDbContext dbContext) : IRequestHandler<MarkRunCancelledCommand>
{
    public async Task Handle(MarkRunCancelledCommand request, CancellationToken cancellationToken)
    {
        var run = await dbContext.Runs.FirstOrDefaultAsync(r => r.PoId == request.PoId, cancellationToken);
        run?.MarkCancelled();
    }
}

public sealed class MarkRunClosedCommandHandler(IProductionDbContext dbContext) : IRequestHandler<MarkRunClosedCommand>
{
    public async Task Handle(MarkRunClosedCommand request, CancellationToken cancellationToken)
    {
        var run = await dbContext.Runs.FirstOrDefaultAsync(r => r.PoId == request.PoId, cancellationToken);
        run?.MarkClosed();
    }
}

public sealed class RefreshRunTermsCommandHandler(IProductionDbContext dbContext) : IRequestHandler<RefreshRunTermsCommand>
{
    public async Task Handle(RefreshRunTermsCommand request, CancellationToken cancellationToken)
    {
        var run = await dbContext.Runs.Include(r => r.PpRounds).FirstOrDefaultAsync(r => r.PoId == request.PoId, cancellationToken);
        run?.ApplyRevisionInForce(request.LatestAcceptableDate, request.IsConstructionChange);
    }
}
