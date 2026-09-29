using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.Revisions;

/// <summary>
/// SCRUM-93 task 27 (AC-34). Full revision history, oldest first, each already carrying its own
/// before/after/diff impact figures against whatever was In-force when it was created (task 20/22).
/// Communication details and evidence aren't returned yet - vendor communication capture (task 30+)
/// and spec files (task 34+) don't exist as concepts in this query's data source yet.
/// </summary>
public sealed record GetPurchaseOrderRevisionsQuery(long PoId) : IRequest<IReadOnlyCollection<PoRevisionDto>>;

public sealed class GetPurchaseOrderRevisionsQueryHandler(IVendorDbContext dbContext)
    : IRequestHandler<GetPurchaseOrderRevisionsQuery, IReadOnlyCollection<PoRevisionDto>>
{
    public async Task<IReadOnlyCollection<PoRevisionDto>> Handle(GetPurchaseOrderRevisionsQuery request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Revisions).ThenInclude(r => r.Lines)
            .Include(p => p.VendorCommunications)
            .FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");

        var evidence = (await dbContext.PurchaseOrderFiles.AsNoTracking()
                .Where(f => f.PoId == po.Id && f.VendorCommunicationId != null && !f.IsDeleted)
                .ToListAsync(cancellationToken))
            .ToLookup(f => f.VendorCommunicationId!.Value);

        return po.Revisions
            .OrderBy(r => r.RevisionNumber)
            .Select(r => r.ToDto() with
            {
                Communications = po.VendorCommunications
                    .Where(c => c.RevisionId == r.Id)
                    .OrderBy(c => c.ResponseDte)
                    .Select(c => new PoRevisionCommunicationDto(
                        c.CommunicationTypeId, c.ChannelId, c.ResponderName, c.ResponseDte,
                        evidence[c.Id].Select(f => new PoEvidenceDto(f.Id, f.FileName)).ToList()))
                    .ToList(),
            })
            .ToList();
    }
}
