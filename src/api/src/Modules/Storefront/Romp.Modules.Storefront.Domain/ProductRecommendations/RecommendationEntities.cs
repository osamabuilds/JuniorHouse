using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Storefront.Domain.ProductRecommendations;

/// <summary>STFR.PRDT_SMLR: a precomputed similar product, rank 1 being the closest (REC-2, REC-3). Holds ids only; names and prices come from the live read model.</summary>
public sealed class SimilarProduct : Entity<long>, IAuditable
{
    /// <summary>Most similar products stored for one product.</summary>
    public const int MaxRank = 12;

    private SimilarProduct()
    {
    }

    public SimilarProduct(long productId, long similarProductId, int rank, double score, DateTimeOffset calculatedAt)
    {
        if (productId <= 0 || similarProductId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(productId), "Product ids must be positive.");
        }

        if (productId == similarProductId)
        {
            throw new ArgumentException("A product cannot be similar to itself.", nameof(similarProductId));
        }

        if (rank is < 1 or > MaxRank)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), $"Rank must be 1 to {MaxRank}.");
        }

        if (double.IsNaN(score) || score is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be 0 to 1.");
        }

        ProductId = productId;
        SimilarProductId = similarProductId;
        Rank = (short)rank;
        Score = (decimal)Math.Round(score, 6);
        CalculatedAt = calculatedAt;
    }

    public long ProductId { get; private set; }

    public long SimilarProductId { get; private set; }

    public short Rank { get; private set; }

    public decimal Score { get; private set; }

    public DateTimeOffset CalculatedAt { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.PRDT_SALE_DAY: units of a product sold on one UTC day, copied from Orders through its contract (REC-4).</summary>
public sealed class ProductSaleDay : Entity<long>, IAuditable
{
    private ProductSaleDay()
    {
    }

    public ProductSaleDay(long productId, DateOnly day, int units)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);

        ProductId = productId;
        Day = day;
        SetUnits(units);
    }

    public long ProductId { get; private set; }

    public DateOnly Day { get; private set; }

    public int Units { get; private set; }

    public void SetUnits(int units)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        Units = units;
    }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.RECM_RUN: one worker pass. No end date means it did not finish; the latest finished run is the watermark for what changed.</summary>
public sealed class RecommendationRun : Entity<long>, IAuditable
{
    private RecommendationRun()
    {
    }

    public RecommendationRun(DateTimeOffset startedAt) => StartedAt = startedAt;

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public int GroupCount { get; private set; }

    public int ProductCount { get; private set; }

    public bool IsFinished => EndedAt is not null;

    public void Finish(DateTimeOffset at, int groupCount, int productCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(groupCount);
        ArgumentOutOfRangeException.ThrowIfNegative(productCount);
        EndedAt = at;
        GroupCount = groupCount;
        ProductCount = productCount;
    }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.RECM_ROW_LKP ids: the two rows on a product page (REC-12).</summary>
public enum RecommendationRowId : short
{
    Similar = 1,
    Trending = 2,
}

/// <summary>STFR.RECM_ROW_LKP</summary>
public sealed class RecommendationRowLookup : StorefrontLookup<RecommendationRowId>;

/// <summary>STFR.RECM_EVNT_DAY: clicks and adds from one row for one product on one day. Counters only: no person, cookie or address (REC-12).</summary>
public sealed class RecommendationDay : Entity<long>, IAuditable
{
    private RecommendationDay()
    {
    }

    public RecommendationDay(RecommendationRowId row, long productId, DateOnly day)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);

        Row = row;
        ProductId = productId;
        Day = day;
    }

    public RecommendationRowId Row { get; private set; }

    public long ProductId { get; private set; }

    public DateOnly Day { get; private set; }

    public int ClickCount { get; private set; }

    public int AddCount { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
