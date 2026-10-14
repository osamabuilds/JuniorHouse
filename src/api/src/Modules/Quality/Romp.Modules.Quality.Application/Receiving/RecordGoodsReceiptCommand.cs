using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Domain.Receiving;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Quality.Application.Receiving;

/// <summary>
/// Records a goods receipt against one inbound delivery (FR-SC-04, BRD 5.11, spec AC-3..AC-12). The GRN, its
/// <c>GoodsReceipt.Recorded</c> outbox event and the QC batch are written in one transaction, so a notification or
/// consumer problem can never roll the receipt back (NFR-FT-08).
/// </summary>
public sealed record RecordGoodsReceiptCommand(
    long InboundDeliveryId,
    DateOnly ReceivedDate,
    string ActingPerson,
    string? Note,
    IReadOnlyList<CountedLineDto> Lines) : IRequest<GoodsReceiptDto>, IQualityCommand;

public sealed class RecordGoodsReceiptCommandValidator : AbstractValidator<RecordGoodsReceiptCommand>
{
    public RecordGoodsReceiptCommandValidator()
    {
        RuleFor(c => c.ActingPerson).ActingPerson();
        RuleFor(c => c.Note).MaximumLength(500).WithMessage("The note can be at most 500 characters.");
        RuleFor(c => c.Lines).NotEmpty().WithMessage("Enter the counted quantity for at least one size and colour.");
        RuleForEach(c => c.Lines).ChildRules(line =>
            line.RuleFor(l => l.Qty).GreaterThanOrEqualTo(0).WithMessage("Each counted quantity must be a whole number of 0 or more."));
    }
}

public sealed class RecordGoodsReceiptCommandHandler(
    IQualityDbContext dbContext,
    IPurchaseOrderQueries poQueries,
    IGrnNumberAllocator numberAllocator,
    IPublisher publisher,
    QualityOptions options,
    TimeProvider timeProvider)
    : IRequestHandler<RecordGoodsReceiptCommand, GoodsReceiptDto>
{
    // PO_STS_LKP id of Cancelled (a system-owned lookup; see Vendor's PoStatus).
    private const short PoCancelled = 4;

    public async Task<GoodsReceiptDto> Handle(RecordGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        var delivery = await dbContext.Deliveries.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == request.InboundDeliveryId, cancellationToken)
            ?? throw new KeyNotFoundException($"Inbound delivery {request.InboundDeliveryId} was not found.");

        if (await dbContext.GoodsReceipts.AnyAsync(g => g.DeliveryId == delivery.Id, cancellationToken))
        {
            throw new DomainException($"Delivery note {delivery.VendorDnNo} already has a goods receipt; only one can be recorded per delivery note.");
        }

        // One contract call: the PO's in-force lines say which sizes and colours may be received even if the vendor did not claim them.
        var terms = await poQueries.GetInForceTermsAsync(delivery.PoId, cancellationToken);
        if (terms?.StatusId == PoCancelled)
        {
            throw new DomainException($"Purchase order {delivery.PoNo} is cancelled, so no goods receipt can be recorded.");
        }

        var poLines = (terms?.Lines ?? []).Select(l => (l.SizeId, l.ColourId)).ToHashSet();
        var grnNo = await numberAllocator.AllocateAsync(cancellationToken);
        var receipt = GoodsReceipt.Open(
            delivery, grnNo, request.ReceivedDate, request.ActingPerson, request.Note,
            request.Lines.Select(l => new CountedLine(l.SizeId, l.ColourId, l.Qty)).ToList(),
            poLines, PakistanClock.Today(timeProvider), options.ReceiptFutureDays);

        dbContext.GoodsReceipts.Add(receipt);
        await dbContext.SaveChangesAsync(cancellationToken); // the id now exists, so the event can carry it

        receipt.RaiseRecorded();
        await publisher.Publish(new GoodsReceiptRecordedNotification(receipt.Id), cancellationToken);
        return receipt.ToDto();
    }
}
