using Romp.Modules.Storefront.Domain.Search;

namespace Romp.Modules.Storefront.Application.Search;

/// <summary>Search configuration, behind a port so Application never reads configuration itself (search plan, Settings).</summary>
public interface ISearchSettings
{
    /// <summary>Most products one search considers; a longer tail is cut after ranking.</summary>
    int MaxHits { get; }

    /// <summary>Below this many full-text hits the query also looks for near spellings.</summary>
    int MinFullTextHits { get; }

    /// <summary>Minimum trigram word-similarity (0 to 1) of a near spelling.</summary>
    double SimilarityThreshold { get; }

    /// <summary>Most items autosuggest returns.</summary>
    int SuggestLimit { get; }

    /// <summary>Zero-result search counts older than this many days are deleted (SRC-13).</summary>
    int MissRetentionDays { get; }
}

/// <summary>A product the index found, with how well it matched (higher is better).</summary>
public sealed record SearchHit(long ProductId, double Rank);

/// <summary>
/// The PostgreSQL text search over the read model. Implemented in Infrastructure with parameterised SQL; the text it receives has already
/// passed the tokeniser, and is passed as a parameter, never joined into SQL (SRC-3).
/// </summary>
public interface IProductSearchIndex
{
    /// <summary>Listable products whose search vector matches <paramref name="tsQuery"/>, best first, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<SearchHit>> FullTextAsync(string tsQuery, int limit, CancellationToken cancellationToken);

    /// <summary>Listable products whose search text is similar to <paramref name="text"/> (tolerates a slip), best first.</summary>
    Task<IReadOnlyList<SearchHit>> SimilarAsync(string text, double threshold, int limit, CancellationToken cancellationToken);
}

/// <summary>The synonym groups in memory for 60 seconds, dropped at once when staff change them (SRC-12).</summary>
public interface ISynonymCache
{
    Task<SynonymExpansion> GetOrLoadAsync(Func<CancellationToken, Task<SynonymExpansion>> load, CancellationToken cancellationToken);

    void Invalidate();
}
