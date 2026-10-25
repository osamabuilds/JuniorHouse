using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Domain.Search;

namespace Romp.Modules.Storefront.Application.PublishedCatalog;

public enum ProjectionOutcome
{
    /// <summary>The snapshot was not newer than what is stored (a repeat or an older one); nothing changed.</summary>
    Unchanged = 0,
    Created = 1,
    Updated = 2,
    Removed = 3,

    /// <summary>A removal for a product the read model never had.</summary>
    Absent = 4,
}

/// <summary>
/// Projects one published-product snapshot into the read model (PCT-1, 2, 4, 5). A null <paramref name="Snapshot"/> means the product is
/// not published (any more) and removes it with every row it owns. The snapshot is the full current state, fetched by the caller (an
/// event consumer, the reconciler or a rebuild), so applying it twice, or out of order, converges (version-wins, <see cref="VersionedSnapshot"/>).
/// <paramref name="RebuildRunId"/> marks a rebuild: the row is recomputed even at the same version and stamped, so the sweep can tell it was visited (PCT-6).
/// </summary>
public sealed record ApplyProductSnapshotCommand(long ProductId, PublishedProductSnapshot? Snapshot, long? RebuildRunId = null)
    : IRequest<ProjectionOutcome>, IStorefrontCommand;

public sealed class ApplyProductSnapshotCommandHandler(IStorefrontReadDbContext db, IReadModelSettings settings, ICatalogLookupQueries lookups)
    : IRequestHandler<ApplyProductSnapshotCommand, ProjectionOutcome>
{
    public async Task<ProjectionOutcome> Handle(ApplyProductSnapshotCommand request, CancellationToken cancellationToken)
    {
        var snapshot = request.Snapshot;
        if (snapshot is not null && snapshot.ProductId != request.ProductId)
        {
            throw new InvalidOperationException($"Snapshot of product {snapshot.ProductId} was sent for product {request.ProductId}.");
        }

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);
        if (snapshot is null)
        {
            return Remove(product);
        }

        // A rebuild recomputes the row even at the same version: the derivation, not the content, may be what changed (PCT-6).
        var forced = request.RebuildRunId is not null && product is not null && snapshot.Version == product.ContentVersion;
        if (product is not null && !forced && !VersionedSnapshot.Replaces(snapshot.Version, product.ContentVersion))
        {
            return ProjectionOutcome.Unchanged;
        }

        if (snapshot.Version < 1)
        {
            // The producer did not say which version this is; storing it would let any later snapshot be mistaken for older (PCT-5).
            throw new InvalidOperationException($"Snapshot of product {snapshot.ProductId} has no content version.");
        }

        var created = product is null;
        if (product is null)
        {
            product = NewProduct(snapshot);
            db.Products.Add(product);
        }
        else
        {
            product.Replace(
                snapshot.Version, snapshot.Path, snapshot.Name, snapshot.Description, snapshot.StyleCode,
                snapshot.Gender.Code, snapshot.Gender.Name, snapshot.Category.Code, snapshot.Category.Name,
                snapshot.AgeBracket.Code, snapshot.AgeBracket.Name, snapshot.PublishedAt, snapshot.CareNote);
        }

        product.MarkRebuilt(request.RebuildRunId);

        var variants = await SyncVariantsAsync(snapshot, cancellationToken);
        var images = await SyncImagesAsync(snapshot, cancellationToken);
        await ReplaceChildrenAsync(snapshot, cancellationToken);

        product.SetSearchText(SearchText.Build(
            snapshot.Name, snapshot.StyleCode, snapshot.Gender.Name, snapshot.Category.Name, snapshot.AgeBracket.Name,
            snapshot.Collections.Select(c => c.Name), snapshot.Variants.Select(v => v.Colour.Name)));

        ReadModelProjector.Recompute(product, variants, images.Images, images.Derivatives, settings);
        return created ? ProjectionOutcome.Created : ProjectionOutcome.Updated;
    }

    private static PublishedProduct NewProduct(PublishedProductSnapshot s) =>
        new(
            s.ProductId, s.Version, s.Path, s.Name, s.Description, s.StyleCode,
            s.Gender.Code, s.Gender.Name, s.Category.Code, s.Category.Name, s.AgeBracket.Code, s.AgeBracket.Name,
            s.PublishedAt, s.CareNote);

    /// <summary>Removes the product and every row it owns. The database cascades from the product (migration), so one delete is enough.</summary>
    private ProjectionOutcome Remove(PublishedProduct? product)
    {
        if (product is null)
        {
            return ProjectionOutcome.Absent;
        }

        db.Products.Remove(product);
        return ProjectionOutcome.Removed;
    }

    /// <summary>Updates variants in place by Catalog's variant id (keeping their stock), adds new ones with any stock already received, drops the rest.</summary>
    private async Task<List<VariantRow>> SyncVariantsAsync(PublishedProductSnapshot s, CancellationToken cancellationToken)
    {
        var existing = await db.Variants.Where(v => v.ProductId == s.ProductId).ToListAsync(cancellationToken);
        var incomingIds = s.Variants.Select(v => v.VariantId).ToHashSet();
        db.Variants.RemoveRange(existing.Where(v => !incomingIds.Contains(v.Id)));

        var skus = s.Variants.Where(v => v.SkuCode is not null).Select(v => v.SkuCode!).ToList();
        var stock = skus.Count == 0
            ? []
            : await db.Stock.Where(r => skus.Contains(r.SkuCode)).ToListAsync(cancellationToken);

        // The snapshot names a size but not its place in the size order; that is the Reference lookup's SORT_SEQ.
        var sizeOrder = (await lookups.ListSizesAsync(cancellationToken)).ToDictionary(l => l.Id, l => l.SortSeq);
        short SizeSeq(PublishedVariant v) => sizeOrder.TryGetValue(v.Size.Id, out var seq) ? seq : short.MaxValue;

        var result = new List<VariantRow>(s.Variants.Count);
        foreach (var v in s.Variants)
        {
            var row = existing.Find(e => e.Id == v.VariantId);
            if (row is null)
            {
                row = new VariantRow(
                    v.VariantId, s.ProductId, v.Colour.Id, v.Colour.Code, v.Colour.Name, v.Size.Id, v.Size.Code, v.Size.Name,
                    SizeSeq(v), v.SkuCode, v.Price, v.CompareAtPrice, v.IsSellable);
                db.Variants.Add(row);
            }
            else
            {
                row.Replace(
                    v.Colour.Id, v.Colour.Code, v.Colour.Name, v.Size.Id, v.Size.Code, v.Size.Name,
                    SizeSeq(v), v.SkuCode, v.Price, v.CompareAtPrice, v.IsSellable);
            }

            var held = v.SkuCode is null ? null : stock.Find(r => string.Equals(r.SkuCode, v.SkuCode, StringComparison.OrdinalIgnoreCase));
            if (held is not null)
            {
                row.ApplyStock(held.Quantity, held.StockVersion);
            }

            result.Add(row);
        }

        return result;
    }

    private sealed record ImageSync(List<ImageRow> Images, List<DerivativeRow> Derivatives);

    /// <summary>
    /// Updates images in place by Catalog's image id, drops the removed ones, and brings each image's derivative queue to the configured
    /// widths × formats. An image whose original changed starts over, so the old (hashed) files are never reused for the new picture.
    /// </summary>
    private async Task<ImageSync> SyncImagesAsync(PublishedProductSnapshot s, CancellationToken cancellationToken)
    {
        var existing = await db.Images.Where(i => i.ProductId == s.ProductId).ToListAsync(cancellationToken);
        var existingIds = existing.Select(i => i.Id).ToList();
        var derivatives = existingIds.Count == 0
            ? []
            : await db.Derivatives.Where(d => existingIds.Contains(d.ImageId)).ToListAsync(cancellationToken);

        var incomingIds = s.Images.Select(i => i.ImageId).ToHashSet();
        db.Images.RemoveRange(existing.Where(i => !incomingIds.Contains(i.Id)));
        db.Derivatives.RemoveRange(derivatives.Where(d => !incomingIds.Contains(d.ImageId)));
        derivatives.RemoveAll(d => !incomingIds.Contains(d.ImageId));

        var mainId = s.Images.OrderBy(i => i.SortSeq).ThenBy(i => i.ImageId).Select(i => (long?)i.ImageId).FirstOrDefault();
        var images = new List<ImageRow>(s.Images.Count);
        foreach (var i in s.Images)
        {
            var isMain = i.ImageId == mainId;
            var row = existing.Find(e => e.Id == i.ImageId);
            if (row is null)
            {
                row = new ImageRow(i.ImageId, s.ProductId, i.ColourId, i.StorageKey, i.Width, i.Height, i.AltText, i.SortSeq, isMain);
                db.Images.Add(row);
            }
            else
            {
                var originalChanged = !string.Equals(row.FileKey, i.StorageKey, StringComparison.Ordinal);
                row.Replace(i.ColourId, i.StorageKey, i.Width, i.Height, i.AltText, i.SortSeq, isMain);
                if (originalChanged)
                {
                    db.Derivatives.RemoveRange(derivatives.Where(d => d.ImageId == i.ImageId));
                    derivatives.RemoveAll(d => d.ImageId == i.ImageId);
                }
            }

            images.Add(row);
            QueueMissingDerivatives(i.ImageId, derivatives);
        }

        return new ImageSync(images, derivatives);
    }

    private void QueueMissingDerivatives(long imageId, List<DerivativeRow> derivatives)
    {
        var wanted = settings.ImageWidths.SelectMany(w => ReadModelProjector.Formats, (w, f) => (Width: w, Format: f)).ToHashSet();
        foreach (var stale in derivatives.Where(d => d.ImageId == imageId && !wanted.Contains((d.Width, d.Format))).ToList())
        {
            db.Derivatives.Remove(stale);
            derivatives.Remove(stale);
        }

        var have = derivatives.Where(d => d.ImageId == imageId).Select(d => (d.Width, d.Format)).ToHashSet();
        foreach (var (width, format) in wanted.Where(w => !have.Contains(w)))
        {
            var row = new DerivativeRow(imageId, width, format);
            db.Derivatives.Add(row);
            derivatives.Add(row);
        }
    }

    /// <summary>Composition, care and collections carry no identity of their own: they are replaced wholesale by the snapshot's lines.</summary>
    private async Task ReplaceChildrenAsync(PublishedProductSnapshot s, CancellationToken cancellationToken)
    {
        await db.Composition.Where(c => c.ProductId == s.ProductId).ExecuteDeleteAsync(cancellationToken);
        await db.CareInstructions.Where(c => c.ProductId == s.ProductId).ExecuteDeleteAsync(cancellationToken);
        await db.Collections.Where(c => c.ProductId == s.ProductId).ExecuteDeleteAsync(cancellationToken);

        db.Composition.AddRange(s.Composition.Select((c, i) => new CompositionRow(s.ProductId, c.Fibre.Code, c.Fibre.Name, c.Percent, i)));
        db.CareInstructions.AddRange(s.Care.Select((c, i) => new CareRow(s.ProductId, c.Care.Code, c.Care.Name, c.SymbolCode, i)));
        db.Collections.AddRange(s.Collections.Select(c => new CollectionRow(s.ProductId, c.Code, c.Name)));
    }
}
