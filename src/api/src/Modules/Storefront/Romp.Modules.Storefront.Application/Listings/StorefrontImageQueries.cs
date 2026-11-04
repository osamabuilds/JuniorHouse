using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Contracts;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Application.Listings;

/// <summary>The Storefront side of <see cref="IStorefrontImageQueries"/>: one query for any number of images.</summary>
public sealed class StorefrontImageQueries(IStorefrontReadDbContext db) : IStorefrontImageQueries
{
    public async Task<IReadOnlyList<ImageThumbnail>> GetThumbnailsAsync(IReadOnlyCollection<long> imageIds, int minWidth, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageIds);
        if (imageIds.Count > IStorefrontImageQueries.MaxIds)
        {
            throw new ArgumentException($"At most {IStorefrontImageQueries.MaxIds} images can be asked for at once.", nameof(imageIds));
        }

        var ids = imageIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var files = await db.Derivatives.AsNoTracking()
            .Where(d => ids.Contains(d.ImageId) && d.Status == DerivativeStatus.Done && d.FileKey != null)
            .ToListAsync(cancellationToken);

        var result = new List<ImageThumbnail>();
        foreach (var group in files.GroupBy(f => f.ImageId))
        {
            var widths = group.Select(f => f.Width).Distinct().Order().ToList();
            var width = widths.FirstOrDefault(w => w >= minWidth);
            if (width == 0)
            {
                width = widths[^1];
            }

            var atWidth = group.Where(f => f.Width == width).ToList();
            var webp = atWidth.FirstOrDefault(f => f.Format == DerivativeFormat.Webp)?.FileKey;
            var jpeg = atWidth.FirstOrDefault(f => f.Format == DerivativeFormat.Jpeg)?.FileKey;
            if (webp is null && jpeg is null)
            {
                continue;
            }

            result.Add(new ImageThumbnail(group.Key, "/media/" + (webp ?? jpeg), jpeg is null ? null : "/media/" + jpeg, width));
        }

        return result;
    }
}
