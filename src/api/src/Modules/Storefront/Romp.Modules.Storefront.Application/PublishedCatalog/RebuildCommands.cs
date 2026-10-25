using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Application.PublishedCatalog;

/// <summary>A rebuild run as an operator sees it (PCT-6). <c>Status</c> is the lookup code: RUNNING, COMPLETED or FAILED.</summary>
public sealed record RebuildRunDto(
    long Id, string Status, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, int ProductCount, int ChartCount, int StockCount, string Actor);

/// <summary>
/// Records that a rebuild has started and returns its id; the work itself is <see cref="RebuildStorefrontCommand"/>, run by the worker
/// (HTTP) or inline (operator CLI). Only one rebuild runs at a time: a second request is refused rather than queued behind it.
/// </summary>
public sealed record StartRebuildCommand(string Actor) : IRequest<long>, IStorefrontCommand;

public sealed class StartRebuildCommandHandler(IStorefrontReadDbContext db, TimeProvider clock) : IRequestHandler<StartRebuildCommand, long>
{
    public async Task<long> Handle(StartRebuildCommand request, CancellationToken cancellationToken)
    {
        if (await db.RebuildRuns.AnyAsync(r => r.Status == RebuildStatus.Running, cancellationToken))
        {
            throw new DomainException("A rebuild of the storefront is already running.");
        }

        var run = new RebuildRun(clock.GetUtcNow(), request.Actor);
        db.RebuildRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken); // the run id is needed by the caller before the command's transaction ends
        return run.Id;
    }
}

public sealed record GetRebuildRunQuery(long RunId) : IRequest<RebuildRunDto?>;

public sealed class GetRebuildRunQueryHandler(IStorefrontReadDbContext db) : IRequestHandler<GetRebuildRunQuery, RebuildRunDto?>
{
    public async Task<RebuildRunDto?> Handle(GetRebuildRunQuery request, CancellationToken cancellationToken) =>
        await db.RebuildRuns.AsNoTracking()
            .Where(r => r.Id == request.RunId)
            .Select(r => new RebuildRunDto(
                r.Id,
                r.Status == RebuildStatus.Running ? "RUNNING" : r.Status == RebuildStatus.Completed ? "COMPLETED" : "FAILED",
                r.StartedAt, r.EndedAt, r.ProductCount, r.ChartCount, r.StockCount, r.Actor))
            .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>Marks a run failed (the work threw, or the process died while it ran). The old rows are untouched, so the storefront keeps serving.</summary>
public sealed record FailRebuildCommand(long? RunId = null) : IRequest<int>;

public sealed class FailRebuildCommandHandler(IStorefrontReadDbContext db, TimeProvider clock) : IRequestHandler<FailRebuildCommand, int>
{
    public async Task<int> Handle(FailRebuildCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await db.RebuildRuns
            .Where(r => r.Status == RebuildStatus.Running && (request.RunId == null || r.Id == request.RunId))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RebuildStatus.Failed).SetProperty(r => r.EndedAt, now), cancellationToken);
    }
}

/// <summary>
/// Rebuilds the whole read model from the Catalog and Warehouse contracts (PCT-6) without emptying it first, so the old rows keep serving
/// until each is replaced: every published product is re-applied (recomputed even at the same version) and stamped with the run id, charts
/// and stock are re-applied, and then the rows no run visited are checked against Catalog one by one (a product published meanwhile by an
/// event is kept; one Catalog no longer publishes is removed). It ends by refreshing the lookup copies and recording the model version.
/// </summary>
public sealed record RebuildStorefrontCommand(long RunId, int PageSize = 200) : IRequest<RebuildRunDto>;

public sealed class RebuildStorefrontCommandHandler(
    IPublishedProductQueries products,
    ISizeChartQueries charts,
    IStockAvailabilityQueries stock,
    IStorefrontReadDbContext db,
    ISender sender,
    TimeProvider clock) : IRequestHandler<RebuildStorefrontCommand, RebuildRunDto>
{
    public async Task<RebuildRunDto> Handle(RebuildStorefrontCommand request, CancellationToken cancellationToken)
    {
        var running = await db.RebuildRuns.AsNoTracking()
            .AnyAsync(r => r.Id == request.RunId && r.Status == RebuildStatus.Running, cancellationToken);
        if (!running)
        {
            throw new DomainException($"Rebuild run {request.RunId} is not running.");
        }

        var productCount = await ApplyProductsAsync(request, cancellationToken);
        var chartCount = await ApplyChartsAsync(request, cancellationToken);
        var stockCount = await ApplyStockAsync(request, cancellationToken);
        await SweepProductsAsync(request.RunId, cancellationToken);
        await sender.Send(new RefreshLookupCopiesCommand(), cancellationToken);

        var run = await db.RebuildRuns.FirstAsync(r => r.Id == request.RunId, cancellationToken);
        run.Complete(clock.GetUtcNow(), productCount, chartCount, stockCount);
        var meta = await db.ModelMeta.FirstAsync(cancellationToken);
        meta.MarkBuilt(ModelMeta.CurrentVersion);
        await db.SaveChangesAsync(cancellationToken);
        db.ClearTracker();

        return (await sender.Send(new GetRebuildRunQuery(request.RunId), cancellationToken))!;
    }

    private async Task<int> ApplyProductsAsync(RebuildStorefrontCommand request, CancellationToken cancellationToken)
    {
        var count = 0;
        var after = 0L;
        while (true)
        {
            var page = await products.ListSnapshotsAsync(after, request.PageSize, cancellationToken);
            foreach (var snapshot in page)
            {
                await sender.Send(new ApplyProductSnapshotCommand(snapshot.ProductId, snapshot, request.RunId), cancellationToken);
                db.ClearTracker();
                count++;
            }

            if (page.Count < request.PageSize)
            {
                return count;
            }

            after = page[^1].ProductId;
        }
    }

    private async Task<int> ApplyChartsAsync(RebuildStorefrontCommand request, CancellationToken cancellationToken)
    {
        var seen = new HashSet<long>();
        var after = 0L;
        while (true)
        {
            var page = await charts.ListChartsAsync(after, request.PageSize, cancellationToken);
            foreach (var chart in page)
            {
                await sender.Send(new ApplySizeChartCommand(chart.ChartId, chart), cancellationToken);
                db.ClearTracker();
                seen.Add(chart.ChartId);
            }

            if (page.Count < request.PageSize)
            {
                break;
            }

            after = page[^1].ChartId;
        }

        var gone = await db.Charts.AsNoTracking().Where(c => !seen.Contains(c.Id)).Select(c => c.Id).ToListAsync(cancellationToken);
        foreach (var id in gone)
        {
            await sender.Send(new ApplySizeChartCommand(id, null), cancellationToken);
            db.ClearTracker();
        }

        return seen.Count;
    }

    private async Task<int> ApplyStockAsync(RebuildStorefrontCommand request, CancellationToken cancellationToken)
    {
        var count = 0;
        var after = 0L;
        while (true)
        {
            var page = await stock.ListAvailabilityAsync(after, request.PageSize, cancellationToken);
            foreach (var item in page)
            {
                await sender.Send(new ApplyAvailabilityCommand(item), cancellationToken);
                db.ClearTracker();
                count++;
            }

            if (page.Count < request.PageSize)
            {
                return count;
            }

            after = page[^1].VariantId;
        }
    }

    /// <summary>Rows no run visited: published meanwhile by an event (kept, and stamped) or no longer published (removed).</summary>
    private async Task SweepProductsAsync(long runId, CancellationToken cancellationToken)
    {
        var unvisited = await db.Products.AsNoTracking()
            .Where(p => p.RebuildRunId == null || p.RebuildRunId != runId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in unvisited)
        {
            var snapshot = await products.GetSnapshotAsync(id, cancellationToken);
            await sender.Send(new ApplyProductSnapshotCommand(id, snapshot, runId), cancellationToken);
            db.ClearTracker();
        }
    }
}
