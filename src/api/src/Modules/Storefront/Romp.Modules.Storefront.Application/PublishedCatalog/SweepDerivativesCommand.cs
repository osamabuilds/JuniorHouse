using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Application.PublishedCatalog;

/// <summary>
/// Removes derivative files no <c>IMG_DRV</c> row references once they are older than the cache lifetime (PCT-10). A changed image gets
/// new hashed names, so its old files become unreferenced; they stay for the lifetime because a cached page may still point at them.
/// Only files whose key is a generated derivative name (<see cref="DerivativeName.TryParse"/>) are ever considered, so originals and
/// anything else in the shared storage are never touched. A file just written by the worker, whose row is not yet committed, is young
/// and so safe. Returns the number removed.
/// </summary>
public sealed record SweepDerivativesCommand : IRequest<int>, IStorefrontCommand;

public sealed class SweepDerivativesCommandHandler(IStorefrontReadDbContext db, IFileStorage storage, IReadModelSettings settings, TimeProvider clock)
    : IRequestHandler<SweepDerivativesCommand, int>
{
    public async Task<int> Handle(SweepDerivativesCommand request, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - settings.DerivativeCacheLifetime;
        var candidates = (await storage.ListAsync(cancellationToken))
            .Where(f => f.ModifiedAt <= cutoff && DerivativeName.TryParse(f.Key, out _, out _, out _))
            .ToList();
        if (candidates.Count == 0)
        {
            return 0;
        }

        var referenced = (await db.Derivatives.AsNoTracking().Where(d => d.FileKey != null).Select(d => d.FileKey!).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var removed = 0;
        foreach (var file in candidates.Where(f => !referenced.Contains(f.Key)))
        {
            await storage.DeleteAsync(file.Key, cancellationToken);
            removed++;
        }

        return removed;
    }
}
