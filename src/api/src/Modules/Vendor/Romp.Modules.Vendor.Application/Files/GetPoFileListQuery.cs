using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.Files;

namespace Romp.Modules.Vendor.Application.Files;

/// <summary>SCRUM-93 task 38 (AC-44). Every non-deleted file, with the revision window each vendor-visible one is effective for.</summary>
public sealed record GetPoFileListQuery(long PoId) : IRequest<IReadOnlyList<PoFileDto>>;

public sealed class GetPoFileListQueryHandler(IVendorDbContext dbContext)
    : IRequestHandler<GetPoFileListQuery, IReadOnlyList<PoFileDto>>
{
    public async Task<IReadOnlyList<PoFileDto>> Handle(GetPoFileListQuery request, CancellationToken cancellationToken)
    {
        if (!await dbContext.PurchaseOrders.AnyAsync(p => p.Id == request.PoId, cancellationToken))
        {
            throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");
        }

        var files = await dbContext.PurchaseOrderFiles.AsNoTracking()
            .Where(f => f.PoId == request.PoId && !f.IsDeleted)
            .OrderBy(f => f.CategoryId).ThenBy(f => f.Id)
            .ToListAsync(cancellationToken);

        var revisionNumbers = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(p => p.Id == request.PoId)
            .SelectMany(p => p.Revisions)
            .ToDictionaryAsync(r => r.Id, r => r.RevisionNumber, cancellationToken);

        short? NumberOf(long? revisionId) =>
            revisionId is not null && revisionNumbers.TryGetValue(revisionId.Value, out var number) ? number : null;

        return files
            .Select(f => new PoFileDto(f.Id, f.FileName, f.CategoryId, PoFileCategory.IsVendorVisible(f.CategoryId),
                f.FileSizeBytes, f.InsrBy, f.InsrDte, NumberOf(f.AddedInRevisionId), NumberOf(f.RetiredInRevisionId)))
            .ToList();
    }
}
