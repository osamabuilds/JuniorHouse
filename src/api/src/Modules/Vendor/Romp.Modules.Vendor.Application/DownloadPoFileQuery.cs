using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Romp.Modules.Vendor.Application;

/// <summary>SCRUM-93 task 36 (AC-46). Viewable/downloadable in every PO status, including Cancelled.</summary>
public sealed record DownloadPoFileQuery(long PoId, long FileId) : IRequest<PoFileDownload>;

/// <summary><see cref="Headers"/> force a download and stop the browser guessing a type - the endpoint applies them verbatim.</summary>
public sealed record PoFileDownload(byte[] Content, string ContentType, string FileName, IReadOnlyDictionary<string, string> Headers);

public sealed class DownloadPoFileQueryHandler(IVendorDbContext dbContext, IFileStorage fileStorage)
    : IRequestHandler<DownloadPoFileQuery, PoFileDownload>
{
    public async Task<PoFileDownload> Handle(DownloadPoFileQuery request, CancellationToken cancellationToken)
    {
        var file = await dbContext.PurchaseOrderFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.FileId && f.PoId == request.PoId && !f.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"File {request.FileId} was not found on purchase order {request.PoId}.");

        var content = await fileStorage.RetrieveAsync(file.StorageKey, cancellationToken);
        var safeName = PoFileContent.SanitiseFileName(file.FileName);

        return new PoFileDownload(content, file.ContentType, safeName, new Dictionary<string, string>
        {
            ["Content-Disposition"] = $"attachment; filename=\"{safeName}\"",
            ["X-Content-Type-Options"] = "nosniff",
        });
    }
}
