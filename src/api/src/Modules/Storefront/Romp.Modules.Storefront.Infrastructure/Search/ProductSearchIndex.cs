using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Storefront.Application.Search;
using Romp.Modules.Storefront.Domain.Search;
using Romp.Modules.Storefront.Infrastructure.Persistence;

namespace Romp.Modules.Storefront.Infrastructure.Search;

/// <summary>
/// Full-text and trigram matching over <c>STFR.PRDT_MAIN</c> (search plan, "Matching"). Every value is a SQL parameter: the visitor's text
/// is never part of the SQL text, and the <c>tsquery</c> string is built from tokens that passed the letters-and-digits allow-list.
/// Only listable products are returned (SRC-14).
/// </summary>
public sealed class ProductSearchIndex(StfrDbContext db) : IProductSearchIndex
{
    public async Task<IReadOnlyList<SearchHit>> FullTextAsync(string tsQuery, int limit, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<HitRow>($"""
            SELECT p."ID" AS "ProductId", ts_rank_cd(p."SRCH_VEC", q.query)::float8 AS "Rank"
            FROM "STFR"."PRDT_MAIN" p, to_tsquery('simple', {tsQuery}) AS q(query)
            WHERE p."VSBL_IND" AND p."SRCH_VEC" @@ q.query
            ORDER BY "Rank" DESC, p."PUBL_DTE" DESC, p."ID" DESC
            LIMIT {limit}
            """).Select(h => new SearchHit(h.ProductId, h.Rank)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SearchHit>> SimilarAsync(string text, double threshold, int limit, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<HitRow>($"""
            SELECT p."ID" AS "ProductId", word_similarity({text}, p."SRCH_TEXT")::float8 AS "Rank"
            FROM "STFR"."PRDT_MAIN" p
            WHERE p."VSBL_IND" AND word_similarity({text}, p."SRCH_TEXT") > {threshold}
            ORDER BY "Rank" DESC, p."PUBL_DTE" DESC, p."ID" DESC
            LIMIT {limit}
            """).Select(h => new SearchHit(h.ProductId, h.Rank)).ToListAsync(cancellationToken);

    private sealed record HitRow(long ProductId, double Rank);
}

/// <summary>Synonym groups held for 60 seconds, loaded by one caller at a time; <see cref="Invalidate"/> drops them when staff edit a group.</summary>
public sealed class SynonymCache(TimeProvider clock) : ISynonymCache, IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private SynonymExpansion? _value;
    private DateTimeOffset _expires;

    public async Task<SynonymExpansion> GetOrLoadAsync(Func<CancellationToken, Task<SynonymExpansion>> load, CancellationToken cancellationToken)
    {
        if (Fresh() is { } hit)
        {
            return hit;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (Fresh() is { } loadedMeanwhile)
            {
                return loadedMeanwhile;
            }

            var loaded = await load(cancellationToken);
            _value = loaded;
            _expires = clock.GetUtcNow().Add(Lifetime);
            return loaded;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _value = null;

    public void Dispose() => _gate.Dispose();

    private SynonymExpansion? Fresh() => _value is { } v && clock.GetUtcNow() < _expires ? v : null;
}

public sealed class SearchSettings(IOptions<StorefrontOptions> options) : ISearchSettings
{
    public int MaxHits => options.Value.SearchMaxHits;

    public int MinFullTextHits => options.Value.SearchMinFullTextHits;

    public double SimilarityThreshold => options.Value.SearchSimilarityThreshold;

    public int SuggestLimit => options.Value.SuggestLimit;
}

/// <summary>Autosuggest answers for 60 seconds per normalised prefix, at most 1,000 prefixes (an entry counts 1 against the limit).</summary>
public sealed class SuggestCache : Application.Search.ISuggestCache, IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 1000 });

    public async Task<Application.Search.SuggestResultDto> GetOrAddAsync(
        string normalisedPrefix, Func<CancellationToken, Task<Application.Search.SuggestResultDto>> create, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(normalisedPrefix, out Application.Search.SuggestResultDto? hit) && hit is not null)
        {
            return hit;
        }

        var made = await create(cancellationToken);
        _cache.Set(normalisedPrefix, made, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = Lifetime });
        return made;
    }

    public void Dispose() => _cache.Dispose();
}
