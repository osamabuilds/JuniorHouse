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

        // SCRUM-93 task 18 (AC-6): a new PO's commercial terms (task 16-17) must be set before it
        // can be sent - field-level errors, not a domain exception, so the admin form can show them
        // against the specific fields (same pattern as CreatePurchaseOrderCommandHandler's checks).
        var errors = new Dictionary<string, string[]>();
        if (po.LatestAcceptableDate is null)
        {
            errors[nameof(po.LatestAcceptableDate)] = ["Latest acceptable date is required before a PO can be sent."];
        }

        if (po.FabricResponsibilityId is null)
        {
            errors[nameof(po.FabricResponsibilityId)] = ["Fabric responsibility is required before a PO can be sent."];
        }

        if (errors.Count > 0)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(errors);
        }

        po.Send();

        // Saved here (not left to VendorTransactionBehavior) so the response DTO's new
        // PoStatusHistoryEntry reflects INSR_DTE/BY as stamped by AuditSaveChangesInterceptor, not
        // the pre-save in-memory value (also true of the outbox row this writes, ADR 0004).
        await dbContext.SaveChangesAsync(cancellationToken);

        return po.ToDto();
    }
}
