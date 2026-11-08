using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.ProductRecommendations;

namespace Romp.Modules.Storefront.Application.ProductRecommendations;

/// <summary>
/// Scores every product of one group (same gender and age group, REC-2) against the others and stores each product's best
/// <see cref="SimilarProduct.MaxRank"/> (REC-3, ADR 0029). The score is the blend of fields, meaning (when a provider answers) and recent
/// sales (REC-13). Every product's rows are replaced in one save, so a page never sees a half-written list. Returns the products written.
/// </summary>
public sealed record RefreshSimilarProductsCommand(string GenderCode, string AgeCode) : IRequest<int>;

public sealed class RefreshSimilarProductsCommandHandler(
    IStorefrontReadDbContext db, ITextSimilarityProvider meaning, IRecommendationSettings settings, TimeProvider clock)
    : IRequestHandler<RefreshSimilarProductsCommand, int>
{
    public async Task<int> Handle(RefreshSimilarProductsCommand request, CancellationToken cancellationToken)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => p.GenderCode == request.GenderCode && p.AgeCode == request.AgeCode && p.IsListable)
            .Select(p => new { p.Id, p.CategoryCode, p.MinPrice })
            .ToListAsync(cancellationToken);
        var ids = products.Select(p => p.Id).ToList();

        var colours = (await db.Variants.AsNoTracking()
                .Where(v => ids.Contains(v.ProductId) && v.IsSellable)
                .Select(v => new { v.ProductId, v.ColourCode })
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToLookup(v => v.ProductId, v => v.ColourCode);

        var since = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1 - settings.SalesWindowDays);
        var units = (await db.ProductSaleDays.AsNoTracking()
                .Where(s => ids.Contains(s.ProductId) && s.Day >= since)
                .GroupBy(s => s.ProductId)
                .Select(g => new { ProductId = g.Key, Units = g.Sum(s => s.Units) })
                .ToListAsync(cancellationToken))
            .ToDictionary(s => s.ProductId, s => s.Units);
        var salesScores = SalesSignal.Normalise(ids.ToDictionary(id => id, id => units.GetValueOrDefault(id)));

        var profiles = products.ToDictionary(
            p => p.Id,
            p => new SimilarityProfile(p.CategoryCode, [.. colours[p.Id]], p.MinPrice));
        var weights = new[] { settings.FieldWeight, settings.MeaningWeight, settings.SalesWeight };

        var now = clock.GetUtcNow();
        var existing = await db.SimilarProducts.Where(s => ids.Contains(s.ProductId)).ToListAsync(cancellationToken);
        db.SimilarProducts.RemoveRange(existing);

        foreach (var product in products)
        {
            var candidates = ids.Where(id => id != product.Id).ToList();
            var meaningScores = meaning.IsAvailable && candidates.Count > 0
                ? await meaning.ScoreAsync(product.Id, candidates, cancellationToken)
                : null;

            var ranked = candidates
                .Select(id => (Id: id, Score: SimilarityBlend.Blend(
                    [FieldSimilarity.Score(profiles[product.Id], profiles[id]), meaningScores is not null && meaningScores.TryGetValue(id, out var m) ? m : null, salesScores[id]],
                    weights)))
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.Id)
                .Take(SimilarProduct.MaxRank)
                .ToList();

            for (var i = 0; i < ranked.Count; i++)
            {
                db.SimilarProducts.Add(new SimilarProduct(product.Id, ranked[i].Id, i + 1, ranked[i].Score, now));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return products.Count;
    }
}
