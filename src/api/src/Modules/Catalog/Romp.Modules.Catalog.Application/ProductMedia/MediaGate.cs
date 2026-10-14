using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Domain.ProductMedia;

namespace Romp.Modules.Catalog.Application.ProductMedia;

/// <summary>What the publish gate needs to know about a product's images (MED-1, MED-6).</summary>
public sealed record MediaGateReport(int ImageCount, IReadOnlyList<string> ImagesNeedingAltText);

/// <summary>The gate reader product-media exposes; Products.Publish calls it, it never calls Products (module plan).</summary>
public interface IMediaGate
{
    Task<MediaGateReport> ReadAsync(long productId, CancellationToken cancellationToken);
}

/// <summary>
/// Reports the image count and names every image whose alt text is missing or not valid, by position and file name (MED-1):
/// "Image 2 (IMG_1234.jpg)". One query.
/// </summary>
public sealed class MediaGate(ICatalogDbContext dbContext) : IMediaGate
{
    public async Task<MediaGateReport> ReadAsync(long productId, CancellationToken cancellationToken)
    {
        var images = await dbContext.Images.AsNoTracking()
            .Where(i => i.ProductId == productId)
            .OrderBy(i => i.SortSeq).ThenBy(i => i.Id)
            .Select(i => new { i.OriginalFileName, i.AltText })
            .ToListAsync(cancellationToken);

        var offenders = images
            .Select((image, index) => (image, position: index + 1))
            .Where(x => AltText.Validate(x.image.AltText) is not null)
            .Select(x => $"Image {x.position} ({x.image.OriginalFileName})")
            .ToList();
        return new MediaGateReport(images.Count, offenders);
    }
}
