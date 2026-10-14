using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Domain.InboundDeliveries;

namespace Romp.Modules.Quality.Application.InboundDeliveries;

/// <summary>
/// Stores QC's copy of a vendor delivery note when PROD records one (spec AC-1). Idempotent by <c>(RunId, VendorDnNo)</c>
/// (AC-2): a repeated message finds the existing row and changes nothing. Returns the inbound delivery id.
/// </summary>
public sealed record RecordInboundDeliveryCommand(
    long RunId,
    long PoId,
    string PoNo,
    string VendorDnNo,
    DateOnly DispatchDate,
    bool IsFinalShipment,
    IReadOnlyList<ClaimedLine> Lines) : IRequest<long>, IQualityCommand;

public sealed class RecordInboundDeliveryCommandHandler(IQualityDbContext dbContext)
    : IRequestHandler<RecordInboundDeliveryCommand, long>
{
    public async Task<long> Handle(RecordInboundDeliveryCommand request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Deliveries.AsNoTracking()
            .Where(d => d.RunId == request.RunId && d.VendorDnNo == request.VendorDnNo)
            .Select(d => (long?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing.Value;
        }

        var delivery = new InboundDelivery(
            request.RunId, request.PoId, request.PoNo, request.VendorDnNo, request.DispatchDate, request.IsFinalShipment, request.Lines);
        dbContext.Deliveries.Add(delivery);
        await dbContext.SaveChangesAsync(cancellationToken);
        return delivery.Id;
    }
}

/// <summary>The PO was cancelled: its open inbound deliveries can no longer be received (AC-5). Idempotent.</summary>
public sealed record MarkInboundDeliveriesCancelledCommand(long PoId) : IRequest, IQualityCommand;

public sealed class MarkInboundDeliveriesCancelledCommandHandler(IQualityDbContext dbContext)
    : IRequestHandler<MarkInboundDeliveriesCancelledCommand>
{
    public async Task Handle(MarkInboundDeliveriesCancelledCommand request, CancellationToken cancellationToken)
    {
        var open = await dbContext.Deliveries.Where(d => d.PoId == request.PoId && !d.IsCancelled).ToListAsync(cancellationToken);
        foreach (var delivery in open)
        {
            delivery.MarkCancelled();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
