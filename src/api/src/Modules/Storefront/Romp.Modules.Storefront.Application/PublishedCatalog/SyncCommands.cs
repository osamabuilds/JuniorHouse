using MediatR;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Application.PublishedCatalog;

// What an event consumer sends: events carry ids (and a version hint) only, so each of these reads the producer's CURRENT state through its
// contract and applies it. Reading current state, not the event's, is what makes duplicate, late and reordered events converge (PCT-5).

/// <summary>A product was published, changed, unpublished or archived: fetch it (null when it is not published) and apply.</summary>
public sealed record SyncProductCommand(long ProductId) : IRequest<ProjectionOutcome>, IStorefrontCommand;

public sealed class SyncProductCommandHandler(IPublishedProductQueries products, ISender sender)
    : IRequestHandler<SyncProductCommand, ProjectionOutcome>
{
    public async Task<ProjectionOutcome> Handle(SyncProductCommand request, CancellationToken cancellationToken)
    {
        var snapshot = await products.GetSnapshotAsync(request.ProductId, cancellationToken);
        return await sender.Send(new ApplyProductSnapshotCommand(request.ProductId, snapshot), cancellationToken);
    }
}

/// <summary>A variant's stock changed: fetch Warehouse's current figure (null when it has never had a posting) and apply.</summary>
public sealed record SyncStockCommand(long VariantId) : IRequest<AvailabilityOutcome>, IStorefrontCommand;

public sealed class SyncStockCommandHandler(IStockAvailabilityQueries stock, ISender sender)
    : IRequestHandler<SyncStockCommand, AvailabilityOutcome>
{
    public async Task<AvailabilityOutcome> Handle(SyncStockCommand request, CancellationToken cancellationToken) =>
        await sender.Send(new ApplyAvailabilityCommand(await stock.GetAsync(request.VariantId, cancellationToken)), cancellationToken);
}

/// <summary>
/// A size chart changed. Catalog has no lookup by chart id, so ask for the chart of the event's category and gender and take it only when
/// it is this chart (the answer may be another chart or the Unisex fallback); anything else means this chart is gone.
/// </summary>
public sealed record SyncSizeChartCommand(long ChartId, short CategoryId, short GenderId) : IRequest<ProjectionOutcome>, IStorefrontCommand;

public sealed class SyncSizeChartCommandHandler(ISizeChartQueries charts, ISender sender)
    : IRequestHandler<SyncSizeChartCommand, ProjectionOutcome>
{
    public async Task<ProjectionOutcome> Handle(SyncSizeChartCommand request, CancellationToken cancellationToken)
    {
        var chart = await charts.GetChartAsync(request.CategoryId, request.GenderId, cancellationToken);
        return await sender.Send(
            new ApplySizeChartCommand(request.ChartId, chart is not null && chart.ChartId == request.ChartId ? chart : null),
            cancellationToken);
    }
}
