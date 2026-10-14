using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Application.StockLedger;

/// <summary>
/// Anti-corruption mapper for Labelling's variant events (module plan): the only file that knows their shape. A malformed event
/// is refused with a reason and never stored.
/// </summary>
public static class VariantEventMapper
{
    private static readonly Regex GtinPattern = new("^[0-9]{13}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SkuPattern = new("^[A-Z0-9][A-Z0-9-]{2,119}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static (RecordVariantAssignedCommand? Command, string? Rejection) Map(VariantGtinAssignedEvent e)
    {
        if (e.VariantId <= 0 || e.StyleId <= 0 || e.ColourId <= 0 || e.SizeId <= 0)
        {
            return (null, "The variant event has a non-positive id.");
        }

        var sku = e.SkuCode ?? string.Empty;
        var gtin = e.Gtin ?? string.Empty;
        if (!SkuPattern.IsMatch(sku))
        {
            return (null, "The variant event has an invalid SKU code.");
        }

        if (!GtinPattern.IsMatch(gtin))
        {
            return (null, "The variant event has an invalid GTIN.");
        }

        return (new RecordVariantAssignedCommand(e.VariantId, e.StyleId, e.ColourId, e.SizeId, sku, gtin), null);
    }

    public static (RecordVariantVoidedCommand? Command, string? Rejection) Map(GtinVoidedEvent e)
    {
        if (e.VariantId <= 0)
        {
            return (null, "The void event has a non-positive variant id.");
        }

        var voidedGtin = e.Gtin ?? string.Empty;
        return GtinPattern.IsMatch(voidedGtin)
            ? (new RecordVariantVoidedCommand(e.VariantId, voidedGtin), null)
            : (null, "The void event has an invalid GTIN.");
    }
}

/// <summary>Records (or updates) warehouse's copy of a variant when Labelling assigns a GTIN. Replays change nothing.</summary>
public sealed record RecordVariantAssignedCommand(long VariantId, long StyleId, short ColourId, short SizeId, string SkuCode, string Gtin)
    : IRequest<long>, IWarehouseCommand;

public sealed class RecordVariantAssignedCommandHandler(IWarehouseDbContext dbContext) : IRequestHandler<RecordVariantAssignedCommand, long>
{
    public async Task<long> Handle(RecordVariantAssignedCommand request, CancellationToken cancellationToken)
    {
        var variant = await dbContext.VariantRefs.FirstOrDefaultAsync(v => v.VariantId == request.VariantId, cancellationToken);
        if (variant is null)
        {
            variant = new VariantRef(request.VariantId, request.StyleId, request.ColourId, request.SizeId, request.SkuCode, request.Gtin);
            dbContext.VariantRefs.Add(variant);
        }
        else
        {
            variant.ApplyAssigned(request.StyleId, request.ColourId, request.SizeId, request.SkuCode, request.Gtin);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return variant.Id;
    }
}

/// <summary>Marks the variant's GTIN as retired when Labelling voids it (D3). A void for a variant we have never seen is kept as a no-op.</summary>
public sealed record RecordVariantVoidedCommand(long VariantId, string Gtin) : IRequest, IWarehouseCommand;

public sealed class RecordVariantVoidedCommandHandler(IWarehouseDbContext dbContext) : IRequestHandler<RecordVariantVoidedCommand>
{
    public async Task Handle(RecordVariantVoidedCommand request, CancellationToken cancellationToken)
    {
        var variant = await dbContext.VariantRefs.FirstOrDefaultAsync(v => v.VariantId == request.VariantId, cancellationToken);
        variant?.ApplyVoided(request.Gtin);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RecordVariantAssignedCommandValidator : AbstractValidator<RecordVariantAssignedCommand>
{
    public RecordVariantAssignedCommandValidator()
    {
        RuleFor(c => c.VariantId).GreaterThan(0);
        RuleFor(c => c.Gtin).Length(13);
    }
}
