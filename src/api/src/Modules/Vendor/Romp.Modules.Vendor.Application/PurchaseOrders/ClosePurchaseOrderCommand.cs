using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;

namespace Romp.Modules.Vendor.Application.PurchaseOrders;

/// <summary>Sprint 3 (D2, AC-25): staff close a Delivered PO. Closing is manual, never automatic; any other status is refused.</summary>
public sealed record ClosePurchaseOrderCommand(long Id) : IRequest<PoDto>, IVendorCommand;

public sealed class ClosePurchaseOrderCommandHandler(IVendorDbContext dbContext) : IRequestHandler<ClosePurchaseOrderCommand, PoDto>
{
    public async Task<PoDto> Handle(ClosePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.Id} was not found.");

        po.Close();

        // Saved here so the response's new history entry carries its audit stamps (same as Cancel).
        await dbContext.SaveChangesAsync(cancellationToken);
        return po.ToDto();
    }
}
