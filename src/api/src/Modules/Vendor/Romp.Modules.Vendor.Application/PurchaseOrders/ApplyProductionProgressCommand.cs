using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;

namespace Romp.Modules.Vendor.Application.PurchaseOrders;

/// <summary>What PROD reported; VNDR turns it into the matching PO transition.</summary>
public enum ProductionProgress
{
    Started,
    PartiallyDelivered,
    Delivered,
}

/// <summary>
/// Sprint 3 (ADR 0008, AC-14, AC-24): applies PROD's decision to the PO. Forward-only and idempotent (a
/// redelivered or out-of-order message never moves the PO backwards), and VNDR validates the transition itself.
/// </summary>
public sealed record ApplyProductionProgressCommand(long PoId, ProductionProgress Progress) : IRequest, IVendorCommand;

public sealed class ApplyProductionProgressCommandHandler(IVendorDbContext dbContext) : IRequestHandler<ApplyProductionProgressCommand>
{
    public async Task Handle(ApplyProductionProgressCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken);

        if (po is null)
        {
            return; // nothing to update; the event outlived the PO row (never happens in practice)
        }

        _ = request.Progress switch
        {
            ProductionProgress.Started => po.StartProduction(),
            ProductionProgress.PartiallyDelivered => po.MarkPartiallyDelivered(),
            _ => po.MarkDelivered(),
        };
    }
}
