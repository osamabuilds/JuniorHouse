using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Storefront.Domain.Search;

/// <summary>STFR.SRCH_SYNM_GRP: words that mean the same to a shopper (<c>frock</c>, <c>dress</c>). Staff edit them as data (SRC-12).</summary>
public sealed class SynonymGroup : Entity<long>, IAuditable
{
    private SynonymGroup() => Name = string.Empty;

    public SynonymGroup(string name, bool isActive = true)
    {
        Name = (name ?? string.Empty).Trim();
        IsActive = isActive;
    }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public void Rename(string name, bool isActive)
    {
        Name = (name ?? string.Empty).Trim();
        IsActive = isActive;
    }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.SRCH_SYNM_TERM: one term of a group, normalised. A term belongs to one group (unique index).</summary>
public sealed class SynonymTerm : Entity<long>, IAuditable
{
    private SynonymTerm() => Term = string.Empty;

    public SynonymTerm(long groupId, string term)
    {
        GroupId = groupId;
        Term = SearchQuery.Normalise(term);
        IsPhrase = Term.Contains(' ', StringComparison.Ordinal);
    }

    public long GroupId { get; private set; }

    public string Term { get; private set; }

    public bool IsPhrase { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>STFR.SRCH_MISS: how often a search text found nothing on a day (SRC-13). Only text: no IP, account or session.</summary>
public sealed class SearchMiss : Entity<long>, IAuditable
{
    private SearchMiss() => QueryText = string.Empty;

    public string QueryText { get; private set; }

    public DateOnly MissDate { get; private set; }

    public int MissCount { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
