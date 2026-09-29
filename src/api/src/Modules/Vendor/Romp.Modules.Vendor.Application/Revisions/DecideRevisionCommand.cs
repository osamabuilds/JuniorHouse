using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.Revisions;

/// <summary>SCRUM-93 task 24 (AC-11, AC-12). Decides an existing Pending revision - Accept moves it In-force (superseding whichever was In-force and updating the PO_MAIN/PO_LINE mirror); Reject leaves the PO's position unchanged.</summary>
public sealed record DecideRevisionCommand(long PoId, short RevisionNumber, bool Accept, string? Note) : IRequest<PoDto>, IVendorCommand;

public sealed class DecideRevisionCommandHandler(IVendorDbContext dbContext)
    : IRequestHandler<DecideRevisionCommand, PoDto>
{
    public async Task<PoDto> Handle(DecideRevisionCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.Revisions).ThenInclude(r => r.Lines)
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");

        if (request.Accept)
        {
            po.AcceptPendingRevision(request.RevisionNumber);
        }
        else
        {
            po.RejectPendingRevision(request.RevisionNumber, request.Note);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return po.ToDto();
    }
}
