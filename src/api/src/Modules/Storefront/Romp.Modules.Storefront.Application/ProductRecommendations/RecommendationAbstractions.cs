namespace Romp.Modules.Storefront.Application.ProductRecommendations;

/// <summary>
/// Closeness of the words of two products (the "meaning" part of the blended score, ADR 0029). A port: release 1 has no provider
/// (<see cref="NoTextSimilarityProvider"/>), and a real one arrives later with its own ADR. It is only ever called by the background refresh, never by a page request.
/// </summary>
public interface ITextSimilarityProvider
{
    bool IsAvailable { get; }

    /// <summary>Scores 0..1 of each candidate against the product; null when the provider cannot answer (the blend then drops the part, REC-13).</summary>
    Task<IReadOnlyDictionary<long, double>?> ScoreAsync(long productId, IReadOnlyCollection<long> candidateIds, CancellationToken cancellationToken);
}

/// <summary>The default everywhere: reports "unavailable", so the refresh runs on fields and sales alone (REC-13).</summary>
public sealed class NoTextSimilarityProvider : ITextSimilarityProvider
{
    public bool IsAvailable => false;

    public Task<IReadOnlyDictionary<long, double>?> ScoreAsync(long productId, IReadOnlyCollection<long> candidateIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<long, double>?>(null);
}

/// <summary>The configuration recommendations need, behind a port so Application never reads configuration itself (Infrastructure binds <c>Storefront:*</c>).</summary>
public interface IRecommendationSettings
{
    /// <summary>Cards in each row: 4 to <see cref="Romp.Modules.Storefront.Domain.ProductRecommendations.SimilarProduct.MaxRank"/>.</summary>
    int RowSize { get; }

    /// <summary>Weight of the field score (category, colours, price) in the blend.</summary>
    double FieldWeight { get; }

    /// <summary>Weight of the meaning score; unused while no provider is available.</summary>
    double MeaningWeight { get; }

    /// <summary>Weight of the recent-sales score.</summary>
    double SalesWeight { get; }

    /// <summary>Days of sales that count as "recent" and as "trending" (REC-4).</summary>
    int SalesWindowDays { get; }

    /// <summary>A pass stops starting new groups once it has run this long; the rest wait for the next pass.</summary>
    TimeSpan RunBudget { get; }

    /// <summary>A group is rescored at least this often, to pick up new sales.</summary>
    TimeSpan GroupMaxAge { get; }
}
