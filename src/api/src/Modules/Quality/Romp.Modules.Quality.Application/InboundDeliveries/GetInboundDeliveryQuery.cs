using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Application.Abstractions;

namespace Romp.Modules.Quality.Application.InboundDeliveries;

public sealed record InboundDeliveryLineDto(short SizeId, short ColourId, int ClaimedQty);

/// <summary>One inbound delivery with what the vendor claims per size and colour: the starting point of a goods receipt (spec AC-1, AC-3).</summary>
public sealed record InboundDeliveryDto(
    long Id,
    long RunId,
    long PoId,
    string PoNo,
    string VendorDnNo,
    DateOnly DispatchDate,
    bool IsFinalShipment,
    bool IsCancelled,
    int ClaimedTotal,
    long? GrnId,
    string? GrnNo,
    IReadOnlyList<InboundDeliveryLineDto> Lines);

/// <summary>Loads one inbound delivery. Unknown id: not found. It carries no PO notes or costs (AC-45).</summary>
public sealed record GetInboundDeliveryQuery(long Id) : IRequest<InboundDeliveryDto>;

public sealed class GetInboundDeliveryQueryHandler(IQualityDbContext dbContext)
    : IRequestHandler<GetInboundDeliveryQuery, InboundDeliveryDto>
{
    public async Task<InboundDeliveryDto> Handle(GetInboundDeliveryQuery request, CancellationToken cancellationToken)
    {
        var delivery = await dbContext.Deliveries.AsNoTracking().Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Inbound delivery {request.Id} was not found.");
        var receipt = await dbContext.GoodsReceipts.AsNoTracking()
            .Where(g => g.DeliveryId == delivery.Id).Select(g => new { g.Id, g.GrnNo }).FirstOrDefaultAsync(cancellationToken);

        return new InboundDeliveryDto(
            delivery.Id, delivery.RunId, delivery.PoId, delivery.PoNo, delivery.VendorDnNo, delivery.DispatchDate, delivery.IsFinalShipment,
            delivery.IsCancelled, delivery.Lines.Sum(l => l.ClaimedQty), receipt?.Id, receipt?.GrnNo,
            delivery.Lines.OrderBy(l => l.SizeId).ThenBy(l => l.ColourId).Select(l => new InboundDeliveryLineDto(l.SizeId, l.ColourId, l.ClaimedQty)).ToList());
    }
}
