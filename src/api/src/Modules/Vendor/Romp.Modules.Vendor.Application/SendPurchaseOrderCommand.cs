using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Romp.Modules.Vendor.Application;

/// <summary>AC-10: Draft -&gt; SentToVendor.</summary>
public sealed record SendPurchaseOrderCommand(long Id) : IRequest<PoDto>, IVendorCommand;

public sealed class SendPurchaseOrderCommandHandler(IVendorDbContext dbContext) : IRequestHandler<SendPurchaseOrderCommand, PoDto>
{
    public async Task<PoDto> Handle(SendPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.Id} was not found.");

        po.Send();

        // Saved here (not left to VendorTransactionBehavior) so the response DTO's new
        // PoStatusHistoryEntry reflects INSR_DTE/BY as stamped by AuditSaveChangesInterceptor, not
        // the pre-save in-memory value (also true of the outbox row this writes, ADR 0004).
        await dbContext.SaveChangesAsync(cancellationToken);

        return po.ToDto();
    }
}
