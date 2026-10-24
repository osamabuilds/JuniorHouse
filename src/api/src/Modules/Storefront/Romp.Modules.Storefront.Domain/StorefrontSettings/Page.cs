using System.Text.RegularExpressions;
using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Storefront.Domain.StorefrontSettings;

/// <summary>
/// A content page (delivery, exchanges, size guide, privacy, ...) made of ordered structured blocks. Block text is plain text and is
/// rendered by bindings only (ST-10). Maps to STFR.PAGE_MAIN.
/// </summary>
public sealed class Page : AggregateRoot<long>, IAuditable
{
    private static readonly Regex SlugPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Slugs that belong to routes of the storefront; a page may not take them (plan, abuse case 5).</summary>
    public static readonly IReadOnlySet<string> ReservedSlugs = new HashSet<string>(StringComparer.Ordinal)
    {
        "girls", "boys", "new-in", "collections", "sale", "search", "p", "api", "admin", "cart", "checkout", "account", "sitemap", "robots", "_foundations",
    };

    private readonly List<PageBlock> _blocks = [];

    private Page()
    {
        Slug = string.Empty;
        Title = string.Empty;
        MetaDescription = string.Empty;
    }

    public Page(string slug, string title, string metaDescription)
    {
        Slug = (slug ?? string.Empty).Trim();
        Title = (title ?? string.Empty).Trim();
        MetaDescription = (metaDescription ?? string.Empty).Trim();
        Status = PageStatus.Draft;
    }

    public string Slug { get; private set; }

    public string Title { get; private set; }

    public string MetaDescription { get; private set; }

    public PageStatus Status { get; private set; }

    public IReadOnlyList<PageBlock> Blocks => _blocks;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>The first problem with a slug (ST-9, abuse case 5), or null when it is acceptable.</summary>
    public static string? CheckSlug(string? slug)
    {
        var value = (slug ?? string.Empty).Trim();
        if (value.Length is < 2 or > 60 || !SlugPattern.IsMatch(value))
        {
            return "Use 2 to 60 lowercase letters, digits and single hyphens for the page address, for example size-guide.";
        }

        return ReservedSlugs.Contains(value) ? $"\"{value}\" is used by the shop itself. Choose another page address." : null;
    }

    public void Rename(string title, string metaDescription)
    {
        Title = (title ?? string.Empty).Trim();
        MetaDescription = (metaDescription ?? string.Empty).Trim();
    }

    /// <summary>Changes the slug and returns the old one so the caller can write a 301 redirect row when the page was published (ST-9).</summary>
    public string ChangeSlug(string newSlug)
    {
        var problem = CheckSlug(newSlug);
        if (problem is not null)
        {
            throw new DomainException(problem);
        }

        var old = Slug;
        Slug = newSlug.Trim();
        return old;
    }

    public void SetStatus(PageStatus status) => Status = status;

    public void ReplaceBlocks(IEnumerable<PageBlock> blocks)
    {
        _blocks.Clear();
        _blocks.AddRange(blocks);
        for (var i = 0; i < _blocks.Count; i++)
        {
            _blocks[i].Place(i + 1);
        }
    }
}

/// <summary>An old slug that now answers with a 301 to the page (ST-9). Maps to STFR.PAGE_RDIR.</summary>
public sealed class PageRedirect : Entity<long>, IAuditable
{
    private PageRedirect() => OldSlug = string.Empty;

    public PageRedirect(string oldSlug, long pageId)
    {
        OldSlug = oldSlug;
        PageId = pageId;
    }

    public string OldSlug { get; private set; }

    public long PageId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
