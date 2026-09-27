using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Romp.Modules.Vendor.Application;

/// <summary>AC-9: Draft only - quantities, cost, date, payment term, advance % all editable in place. AC-13: rejected past Draft.</summary>
public sealed record UpdatePurchaseOrderCommand(
    long Id,
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    short PaymentTermId,
    decimal AdvancePercent,
    IReadOnlyCollection<PoLineDto> Lines) : IRequest<PoDto>, IVendorCommand;

public sealed class UpdatePurchaseOrderCommandValidator : AbstractValidator<UpdatePurchaseOrderCommand>
{
    public UpdatePurchaseOrderCommandValidator()
    {
        RuleFor(c => c.UnitCost).GreaterThan(0);
        RuleFor(c => c.Lines).NotEmpty().WithMessage("At least one size/colour line is required.");
        RuleForEach(c => c.Lines).ChildRules(line => line.RuleFor(l => l.Qty).GreaterThan(0));
    }
}

public sealed class UpdatePurchaseOrderCommandHandler(IVendorDbContext dbContext)
    : IRequestHandler<UpdatePurchaseOrderCommand, PoDto>
{
    public async Task<PoDto> Handle(UpdatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.Id} was not found.");

        po.UpdateDraftDetails(
            request.UnitCost,
            request.ExpectedDeliveryDate,
            request.PaymentTermId,
            request.AdvancePercent,
            request.Lines.Select(l => (l.SizeId, l.ColourId, l.Qty)));

        // Saved here (not left to VendorTransactionBehavior) so the response DTO reflects
        // UPDT_DTE/BY and the new lines' INSR_DTE/BY as stamped by AuditSaveChangesInterceptor,
        // not the pre-save in-memory values.
        await dbContext.SaveChangesAsync(cancellationToken);

        return po.ToDto();
    }
}
