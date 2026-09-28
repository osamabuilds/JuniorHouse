using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Romp.Modules.Vendor.Application;

/// <summary>
/// AC-11 / AC-4: SentToVendor -&gt; Acknowledged. Sprint 1's empty-body acknowledge, kept for
/// compatibility; it now delegates to <see cref="RecordVendorResponseCommandHandler"/> as a
/// Confirmed response against the current In-force revision, channel Unspecified (SCRUM-93 task 32).
/// </summary>
public sealed record AcknowledgePurchaseOrderCommand(long Id) : IRequest<PoDto>, IVendorCommand;

public sealed class AcknowledgePurchaseOrderCommandHandler(IVendorDbContext dbContext, IFileStorage fileStorage)
    : IRequestHandler<AcknowledgePurchaseOrderCommand, PoDto>
{
    private const short ConfirmedOutcomeId = 1;   // PO_VNDR_COMM_TYP_LKP.Confirmed
    private const short UnspecifiedChannelId = 5; // VNDR_COMM_CHNL_LKP.Unspecified
    private const short InForceStatusId = 2;      // PO_REV_STS_LKP.InForce

    public async Task<PoDto> Handle(AcknowledgePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var exists = await dbContext.PurchaseOrders.AnyAsync(p => p.Id == request.Id, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException($"Purchase order {request.Id} was not found.");
        }

        var inForceRevisionNumber = await dbContext.PurchaseOrders
            .Where(p => p.Id == request.Id)
            .SelectMany(p => p.Revisions)
            .Where(r => r.StatusId == InForceStatusId)
            .Select(r => (short?)r.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        // Called directly rather than via ISender: same DbContext/transaction, no nested pipeline.
        var result = await new RecordVendorResponseCommandHandler(dbContext, fileStorage).Handle(
            new RecordVendorResponseCommand(
                request.Id,
                ConfirmedOutcomeId,
                inForceRevisionNumber,
                UnspecifiedChannelId,
                "Not recorded",
                ResponseDte: null,
                Counter: null),
            cancellationToken);

        return result.Po;
    }
}
