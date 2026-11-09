using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Fulfilment.Application.Abstractions;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Domain.PackStation;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Fulfilment.Application.PickLists;

/// <summary>The answer to taking a pick: taken, held by someone else (PCK-9), or no such order.</summary>
public sealed record ClaimResult(bool Claimed, string? HeldBy, bool NotFound);

public sealed record ClaimPickCommand(long OrderId, string Staff) : IRequest<ClaimResult>, IFulfilmentCommand;

public sealed class ClaimPickCommandHandler(IFulfilmentDbContext dbContext, TimeProvider clock) : IRequestHandler<ClaimPickCommand, ClaimResult>
{
    public async Task<ClaimResult> Handle(ClaimPickCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return new ClaimResult(false, null, true);
        }

        var heldBy = order.Claim(request.Staff, clock.GetUtcNow());
        return new ClaimResult(heldBy is null, heldBy, false);
    }
}

/// <summary>The holder lets the pick go back to the queue; a manager may take it over. Scans already made stay counted.</summary>
public sealed record ReleasePickClaimCommand(long OrderId, string Staff, bool IsManager) : IRequest<bool>, IFulfilmentCommand;

public sealed class ReleasePickClaimCommandHandler(IFulfilmentDbContext dbContext) : IRequestHandler<ReleasePickClaimCommand, bool>
{
    public async Task<bool> Handle(ReleasePickClaimCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        return order is not null && order.ReleaseClaim(request.Staff, request.IsManager);
    }
}

/// <summary>What a scan answered, with the state the screen needs next so it need not ask again (APP-6).</summary>
public sealed record ScanOutcomeDto(bool Accepted, string Code, string Message, string? NextBin, string? CurrentBin, bool ReadyToComplete, int PickedUnits, int TotalUnits);

/// <summary>
/// One scan at the pick station: a bin code or a barcode (PCK-2 to PCK-6). The order is loaded once with its allocations; a scan is that read and one
/// update guarded by the allocation quantities, with no call to another module (ADR 0030). A refusal answers in words and changes nothing.
/// </summary>
public sealed record ScanPickCommand(long OrderId, string Staff, string Code) : IRequest<ScanOutcomeDto?>, IFulfilmentCommand;

public sealed class ScanPickCommandHandler(IFulfilmentDbContext dbContext, TimeProvider clock) : IRequestHandler<ScanPickCommand, ScanOutcomeDto?>
{
    public async Task<ScanOutcomeDto?> Handle(ScanPickCommand request, CancellationToken cancellationToken)
    {
        var code = ScanCodes.Clean(request.Code);
        var order = await dbContext.Orders.Include(o => o.Lines).ThenInclude(l => l.Allocations).FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var result = ScanCodes.IsBarcode(code) ? order.ScanUnit(request.Staff, code) : order.ScanBin(request.Staff, code);
        dbContext.Scans.Add(new ScanEntry(order.OrderId, ScanStages.Pick, code, result.Accepted, result.Code, request.Staff, clock.GetUtcNow()));
        var active = order.ActiveLines.ToList();
        return new ScanOutcomeDto(result.Accepted, result.Code, result.Message, result.NextBin ?? order.NextBin(), order.CurrentBin, order.IsReadyToComplete, active.Sum(l => l.PickedQuantity), active.Sum(l => l.Quantity));
    }
}

/// <summary>Completes the pick: each allocated unit is decremented from its bin once and the reservation is converted (PCK-7); a repeat changes nothing (PCK-8).</summary>
public sealed record CompletePickCommand(long OrderId, string Staff) : IRequest<bool?>, IFulfilmentCommand;

public sealed class CompletePickCommandHandler(IFulfilmentDbContext dbContext, IStockAllocator allocator, TimeProvider clock) : IRequestHandler<CompletePickCommand, bool?>
{
    public async Task<bool?> Handle(CompletePickCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.Include(o => o.Lines).ThenInclude(l => l.Allocations).FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var picks = order.CompletePick(request.Staff, clock.GetUtcNow());
        if (picks is null)
        {
            return false;
        }

        // Warehouse writes the decrement in its own transaction and is idempotent on the key: if saving here fails, a retry decrements nothing twice.
        await allocator.ConfirmPickAsync(order.PlacementKey, [.. picks.Select(p => new PickedUnit(p.VariantId, p.BinId, p.Quantity))], request.Staff, cancellationToken);
        return true;
    }
}

/// <summary>Cleans and classifies what a scanner typed. Only letters, digits and dashes can ever reach a query or a log line.</summary>
public static class ScanCodes
{
    public const int MaxLength = 40;

    public static string Clean(string? raw)
    {
        var code = (raw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length is 0 or > MaxLength || !code.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new DomainException("That is not a barcode or a shelf code.");
        }

        return code;
    }

    /// <summary>A barcode is 13 digits; a shelf code always has dashes (A-03-2), so the two can never be confused.</summary>
    public static bool IsBarcode(string code) => code.Length == 13 && code.All(char.IsAsciiDigit);
}
