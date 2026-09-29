using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.PurchaseOrders;

/// <summary>AC-12: Draft/SentToVendor/Acknowledged -&gt; Cancelled, with a mandatory reason. AC-12a: reason is required.</summary>
public sealed record CancelPurchaseOrderCommand(long Id, short CancelReasonId) : IRequest<PoDto>, IVendorCommand;

public sealed class CancelPurchaseOrderCommandValidator : AbstractValidator<CancelPurchaseOrderCommand>
{
    public CancelPurchaseOrderCommandValidator()
    {
        RuleFor(c => c.CancelReasonId).GreaterThan((short)0).WithMessage("A cancellation reason is required.");
    }
}

public sealed class CancelPurchaseOrderCommandHandler(IVendorDbContext dbContext) : IRequestHandler<CancelPurchaseOrderCommand, PoDto>
{
    public async Task<PoDto> Handle(CancelPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.StatusHistory)
            .Include(p => p.Revisions).ThenInclude(r => r.Lines)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.Id} was not found.");

        po.Cancel(request.CancelReasonId);

        // Saved here (not left to VendorTransactionBehavior) so the response DTO's new
        // PoStatusHistoryEntry reflects INSR_DTE/BY as stamped by AuditSaveChangesInterceptor, not
        // the pre-save in-memory value (also true of the outbox row this writes, ADR 0004).
        await dbContext.SaveChangesAsync(cancellationToken);

        return po.ToDto();
    }
}
