using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.PurchaseOrders;

namespace Romp.Modules.Vendor.Application.PurchaseOrders;

/// <summary>
/// FR-SC-02, AC-7: raises a PO against a vendor and style. <see cref="PaymentTermId"/>/
/// <see cref="AdvancePercent"/> are optional - when omitted, the handler defaults them from the
/// vendor's own payment term (AC-7a); the admin form pre-fills them the same way but staff can
/// still override either before submitting.
/// </summary>
public sealed record CreatePurchaseOrderCommand(
    long VendorId,
    long StyleId,
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    IReadOnlyCollection<PoLineDto> Lines,
    short? PaymentTermId = null,
    decimal? AdvancePercent = null) : IRequest<PoDto>, IVendorCommand;

public sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(c => c.UnitCost).GreaterThan(0);
        RuleFor(c => c.Lines).NotEmpty().WithMessage("At least one size/colour line is required.");
        RuleForEach(c => c.Lines).ChildRules(line => line.RuleFor(l => l.Qty).GreaterThan(0));
    }
}

public sealed class CreatePurchaseOrderCommandHandler(
    IVendorDbContext dbContext,
    IStyleQueries styleQueries,
    IPaymentTermQueries paymentTermQueries,
    IPoNumberAllocator poNumberAllocator)
    : IRequestHandler<CreatePurchaseOrderCommand, PoDto>
{
    public async Task<PoDto> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        var vendor = await dbContext.Vendors.FirstOrDefaultAsync(v => v.Id == request.VendorId, cancellationToken);
        if (vendor is null || !vendor.IsActive)
        {
            errors[nameof(request.VendorId)] = ["Vendor must exist and be active."];
        }

        var style = await styleQueries.FindActiveStyleAsync(request.StyleId, cancellationToken);
        if (style is null)
        {
            errors[nameof(request.StyleId)] = ["Style must exist and be active."];
        }

        // AC-8: every line's size and colour must be part of the style's size run/colourways.
        if (style is not null)
        {
            var invalidLines = request.Lines
                .Where(line => !style.SizeIds.Contains(line.SizeId) || !style.ColourIds.Contains(line.ColourId))
                .ToList();

            if (invalidLines.Count > 0)
            {
                errors[nameof(request.Lines)] =
                    [$"{invalidLines.Count} line(s) reference a size or colour not in style '{style.Code}'."];
            }
        }

        if (errors.Count > 0)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(errors);
        }

        var paymentTermId = request.PaymentTermId ?? vendor!.PaymentTermId;
        var advancePercent = request.AdvancePercent
            ?? await paymentTermQueries.GetDefaultAdvancePercentAsync(paymentTermId, cancellationToken)
            ?? 0m;

        var poNo = await poNumberAllocator.AllocateAsync(cancellationToken);

        var po = new Domain.PurchaseOrders.PurchaseOrder(
            poNo,
            request.VendorId,
            request.StyleId,
            request.UnitCost,
            request.ExpectedDeliveryDate,
            paymentTermId,
            advancePercent,
            request.Lines.Select(l => (l.SizeId, l.ColourId, l.Qty)));

        dbContext.PurchaseOrders.Add(po);
        await dbContext.SaveChangesAsync(cancellationToken);

        return po.ToDto();
    }
}
