using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Fulfilment.Application.Abstractions;
using Romp.Modules.Fulfilment.Application.PickLists;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Domain.Allocations;
using Romp.Modules.Fulfilment.Domain.PackStation;

namespace Romp.Modules.Fulfilment.Application.PackStation;

public sealed record PackLineDto(long LineId, string? Sku, string Name, string Colour, string Size, int Quantity, int Packed);

/// <summary>
/// An order at the pack station: what to scan, the packaging to use (PAK-4), the weight to expect, the weight recorded, and the cash to collect shown
/// large for cash on delivery (PAK-11). Order number and amounts only: no customer data.
/// </summary>
public sealed record PackViewDto(
    long OrderId, string OrderNumber, string Status, string? ClaimedBy, bool IsCashOnDelivery, decimal CashToCollect, string Packaging, int ExpectedGrams,
    IReadOnlyList<PackLineDto> Lines, bool AllScanned, int? WeightGrams, bool WeightDeviates, string? Note);

/// <summary>Builds the view and the expected weight and packaging from the settings (Q-FUL-1, Q-FUL-3).</summary>
public sealed class PackPlanner(IOptions<FulfilmentOptions> options)
{
    public int ExpectedGrams(FulfilmentOrder order) => (order.TotalUnits * options.Value.GarmentGrams) + options.Value.PackagingGrams;

    public short Packaging(FulfilmentOrder order) =>
        PackagingRule.Recommend(order.TotalUnits, ExpectedGrams(order), options.Value.PolyBagMaxGrams, options.Value.PolyBagMaxItems);

    public WeightCheckResult Check(FulfilmentOrder order, int grams) =>
        WeightCheck.Evaluate(grams, ExpectedGrams(order), options.Value.WeightTolerancePercent, options.Value.WeightToleranceGrams);

    public PackViewDto View(FulfilmentOrder order) => new(
        order.Id, order.OrderNumber, FulfilmentStatuses.CodeOf(order.StatusId), order.PackClaimedBy, order.IsCashOnDelivery, order.CashToCollect,
        PackagingKinds.CodeOf(Packaging(order)), ExpectedGrams(order),
        [.. order.ActiveLines.OrderBy(l => l.Id).Select(l => new PackLineDto(l.Id, l.Sku, l.Name, l.Colour, l.Size, l.Quantity, l.PackedQuantity))],
        order.ActiveLines.All(l => l.PackedQuantity >= l.Quantity), order.Pack?.WeightGrams, order.Pack?.IsDeviation ?? false, order.Pack?.Note);
}

public sealed record GetPackViewQuery(long OrderId) : IRequest<PackViewDto?>;

public sealed class GetPackViewQueryHandler(IFulfilmentDbContext dbContext, PackPlanner planner) : IRequestHandler<GetPackViewQuery, PackViewDto?>
{
    public async Task<PackViewDto?> Handle(GetPackViewQuery request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.AsNoTracking().Include(o => o.Lines).Include(o => o.Pack).FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        return order is null ? null : planner.View(order);
    }
}

/// <summary>The packer opens a picked order at the pack station. Only a fully picked order can be packed (PAK-10).</summary>
public sealed record OpenPackResult(PackViewDto? View, string? HeldBy);

public sealed record OpenPackCommand(long OrderId, string Staff) : IRequest<OpenPackResult?>, IFulfilmentCommand;

public sealed class OpenPackCommandHandler(IFulfilmentDbContext dbContext, PackPlanner planner) : IRequestHandler<OpenPackCommand, OpenPackResult?>
{
    public async Task<OpenPackResult?> Handle(OpenPackCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.Include(o => o.Lines).Include(o => o.Pack).FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var heldBy = order.OpenPack(request.Staff);
        return new OpenPackResult(heldBy is null ? planner.View(order) : null, heldBy);
    }
}

public sealed record PackScanOutcomeDto(bool Accepted, string Code, string Message, bool AllScanned, int PackedUnits, int TotalUnits);

/// <summary>A second, independent scan of each unit, counted apart from the pick (PAK-1, PAK-2).</summary>
public sealed record ScanPackCommand(long OrderId, string Staff, string Code) : IRequest<PackScanOutcomeDto?>, IFulfilmentCommand;

public sealed class ScanPackCommandHandler(IFulfilmentDbContext dbContext, TimeProvider clock) : IRequestHandler<ScanPackCommand, PackScanOutcomeDto?>
{
    public async Task<PackScanOutcomeDto?> Handle(ScanPackCommand request, CancellationToken cancellationToken)
    {
        var code = ScanCodes.Clean(request.Code);
        var order = await dbContext.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var result = ScanCodes.IsBarcode(code) ? order.ScanPack(request.Staff, code) : new ScanResult(false, "NOT_A_BARCODE", "Scan the barcode on the item.");
        dbContext.Scans.Add(new ScanEntry(order.OrderId, ScanStages.Pack, code, result.Accepted, result.Code, request.Staff, clock.GetUtcNow()));
        var active = order.ActiveLines.ToList();
        return new PackScanOutcomeDto(result.Accepted, result.Code, result.Message, active.All(l => l.PackedQuantity >= l.Quantity), active.Sum(l => l.PackedQuantity), active.Sum(l => l.Quantity));
    }
}

public sealed record WeightOutcomeDto(int WeightGrams, int ExpectedGrams, int AllowedGrams, int DeviationGrams, bool WithinTolerance, string Packaging, string Message);

/// <summary>The parcel is weighed in whole grams (PAK-5). A weight of zero, below zero or over 30 kg is refused (PAK-8); one outside the tolerance warns (PAK-7).</summary>
public sealed record RecordWeightCommand(long OrderId, string Staff, int Grams) : IRequest<WeightOutcomeDto?>, IFulfilmentCommand;

public sealed class RecordWeightCommandHandler(IFulfilmentDbContext dbContext, PackPlanner planner, IOptions<FulfilmentOptions> options, TimeProvider clock)
    : IRequestHandler<RecordWeightCommand, WeightOutcomeDto?>
{
    public async Task<WeightOutcomeDto?> Handle(RecordWeightCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.Include(o => o.Lines).Include(o => o.Pack).FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var settings = options.Value;
        var check = order.RecordWeight(request.Staff, request.Grams, planner.ExpectedGrams(order), planner.Packaging(order), settings.WeightTolerancePercent, settings.WeightToleranceGrams, clock.GetUtcNow());
        var message = check.IsWithinTolerance
            ? "The weight fits the order."
            : $"The weight is {Math.Abs(check.DeviationGrams)} g {(check.DeviationGrams > 0 ? "over" : "under")} what the order should weigh. Check the parcel again.";
        return new WeightOutcomeDto(request.Grams, planner.ExpectedGrams(order), check.AllowedGrams, check.DeviationGrams, check.IsWithinTolerance, PackagingKinds.CodeOf(planner.Packaging(order)), message);
    }
}

/// <summary>Seals the parcel (PAK-9). Needs every unit scanned, a weight, and a note when the weight is off. A repeat changes nothing (PAK-13).</summary>
public sealed record CompletePackCommand(long OrderId, string Staff, string? Note) : IRequest<bool?>, IFulfilmentCommand;

public sealed class CompletePackCommandHandler(IFulfilmentDbContext dbContext, TimeProvider clock) : IRequestHandler<CompletePackCommand, bool?>
{
    public async Task<bool?> Handle(CompletePackCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.Include(o => o.Lines).Include(o => o.Pack).FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        return order?.CompletePack(request.Staff, request.Note, clock.GetUtcNow());
    }
}
