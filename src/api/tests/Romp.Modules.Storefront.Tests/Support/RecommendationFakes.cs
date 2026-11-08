using Romp.Modules.Orders.Contracts;
using Romp.Modules.Storefront.Application.ProductRecommendations;

namespace Romp.Modules.Storefront.Tests.Support;

/// <summary>The real clock until a test fixes it: recommendation tests move time to prove daily refreshes and the 14-day window.</summary>
internal sealed class SettableClock : TimeProvider
{
    public DateTimeOffset? Fixed { get; set; }

    public override DateTimeOffset GetUtcNow() => Fixed ?? DateTimeOffset.UtcNow;
}

/// <summary>Settings a test can change; the defaults are the production ones (ADR 0029).</summary>
internal sealed class FakeRecommendationSettings : IRecommendationSettings
{
    public double FieldWeight { get; set; } = 0.50;

    public double MeaningWeight { get; set; } = 0.35;

    public double SalesWeight { get; set; } = 0.15;

    public int SalesWindowDays { get; set; } = 14;

    public TimeSpan RunBudget { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan GroupMaxAge { get; set; } = TimeSpan.FromDays(1);
}

/// <summary>The meaning provider as a test wants it: off, on with scores it names, or failing for chosen products.</summary>
internal sealed class FakeTextSimilarityProvider : ITextSimilarityProvider
{
    public bool IsAvailable { get; set; }

    /// <summary>Meaning score of a candidate for any product; candidates not named score 0.</summary>
    public Dictionary<long, double> Scores { get; } = [];

    public HashSet<long> FailFor { get; } = [];

    public Task<IReadOnlyDictionary<long, double>?> ScoreAsync(long productId, IReadOnlyCollection<long> candidateIds, CancellationToken cancellationToken)
    {
        if (FailFor.Contains(productId))
        {
            throw new InvalidOperationException("The provider failed.");
        }

        return Task.FromResult<IReadOnlyDictionary<long, double>?>(candidateIds.ToDictionary(id => id, id => Scores.GetValueOrDefault(id)));
    }
}

/// <summary>Orders' sales contract: a test says what was sold; a range filters it the way the real query does.</summary>
internal sealed class FakeProductSales : IProductSalesQueries
{
    public List<ProductDayUnits> Rows { get; } = [];

    public Task<IReadOnlyList<ProductDayUnits>> UnitsSoldByProductAndDayAsync(DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProductDayUnits>>([.. Rows.Where(r => r.Day >= fromDay && r.Day <= toDay)]);
}
