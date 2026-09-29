using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.Files;

namespace Romp.Modules.Vendor.Application.PurchaseOrders;

/// <summary>AC-10: Draft -&gt; SentToVendor.</summary>
/// <param name="SendWithoutTechPack">AC-39: must be true to send a PO that has no TechPackSpec file attached; recorded in the status history.</param>
public sealed record SendPurchaseOrderCommand(long Id, bool SendWithoutTechPack = false) : IRequest<PoDto>, IVendorCommand;

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

        // SCRUM-93 task 41 (AC-39): a missing tech pack is a non-blocking warning - staff can still
        // send, but only by saying so explicitly, and that choice is kept in the status history.
        var hasTechPack = await dbContext.PurchaseOrderFiles.AnyAsync(
            f => f.PoId == po.Id && !f.IsDeleted && f.CategoryId == PoFileCategory.TechPackSpec, cancellationToken);
        if (!hasTechPack && !request.SendWithoutTechPack)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.SendWithoutTechPack)] =
                    [$"PO {po.PoNo} has no Tech Pack Spec attached. Attach one, or confirm you want to send it without a tech pack."],
            });
        }

        po.Send(hasTechPack ? null : "Sent without a Tech Pack Spec attached (confirmed by staff).");

        // Saved here (not left to VendorTransactionBehavior) so the response DTO's new
        // PoStatusHistoryEntry reflects INSR_DTE/BY as stamped by AuditSaveChangesInterceptor, not
        // the pre-save in-memory value (also true of the outbox row this writes, ADR 0004).
        await dbContext.SaveChangesAsync(cancellationToken);

        return po.ToDto();
    }
}
