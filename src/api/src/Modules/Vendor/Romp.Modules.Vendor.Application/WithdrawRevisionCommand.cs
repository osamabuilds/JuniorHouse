using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Romp.Modules.Vendor.Application;

/// <summary>SCRUM-93 task 25 (AC-13). A Pending revision withdrawn by its own proposer - never takes effect.</summary>
public sealed record WithdrawRevisionCommand(long PoId, short RevisionNumber, string? Note) : IRequest<PoDto>, IVendorCommand;

public sealed class WithdrawRevisionCommandHandler(IVendorDbContext dbContext)
    : IRequestHandler<WithdrawRevisionCommand, PoDto>
{
    public async Task<PoDto> Handle(WithdrawRevisionCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.Revisions).ThenInclude(r => r.Lines)
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");

        po.WithdrawPendingRevision(request.RevisionNumber, request.Note);

        await dbContext.SaveChangesAsync(cancellationToken);

        return po.ToDto();
    }
}
