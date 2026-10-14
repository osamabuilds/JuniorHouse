using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Catalog.Domain.Urls;

/// <summary>
/// Every path a product has had (ADR 0013, URL-2, URL-3): one row per (gender, category, slug), unique across all products, so an old
/// address can never be claimed by another product (URL-11). The current path is written here at first publish, so resolving a path is
/// one indexed read of this table. Rows are never deleted. Maps to CTLG.PRDT_SLUG_HIST.
/// </summary>
public sealed class PathHistoryEntry : Entity<long>, IAuditable
{
    private PathHistoryEntry()
    {
        Slug = string.Empty;
    }

    public PathHistoryEntry(long productId, short genderId, short categoryId, string slug)
    {
        ProductId = productId;
        GenderId = genderId;
        CategoryId = categoryId;
        Slug = slug;
    }

    public long ProductId { get; private set; }

    public short GenderId { get; private set; }

    public short CategoryId { get; private set; }

    public string Slug { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
