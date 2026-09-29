using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.Files;

namespace Romp.Modules.Vendor.Application.Files;

/// <summary>SCRUM-93 task 35/39 (AC-40, AC-41, AC-42). Soft-removes a Draft-stage file; later removal is rejected by <see cref="PoFilePolicy"/>.</summary>
public sealed record RemovePoFileCommand(long PoId, long FileId) : IRequest, IVendorCommand;

public sealed class RemovePoFileCommandHandler(IVendorDbContext dbContext) : IRequestHandler<RemovePoFileCommand>
{
    public async Task Handle(RemovePoFileCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");

        var file = await dbContext.PurchaseOrderFiles
            .FirstOrDefaultAsync(f => f.Id == request.FileId && f.PoId == po.Id && !f.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"File {request.FileId} was not found on PO {po.PoNo}.");

        PoFilePolicy.EnsureCanRemove(po.PoNo, po.StatusId, file.CategoryId);

        file.SoftDelete();
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
