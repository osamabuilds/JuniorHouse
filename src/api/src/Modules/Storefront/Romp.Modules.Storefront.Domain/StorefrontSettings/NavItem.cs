using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Storefront.Domain.StorefrontSettings;

/// <summary>
/// One entry of the header navigation tree (up to three levels). A <c>Group</c> only opens its children; a <c>Link</c> has a target and
/// may also have children; a <c>CollectionList</c> lists the enabled collections as children. Maps to STFR.NAV_ITEM.
/// </summary>
public sealed class NavItem : Entity<long>, IAuditable
{
    private readonly List<NavItem> _children = [];

    private NavItem()
    {
        Key = string.Empty;
        Label = string.Empty;
    }

    public NavItem(string key, string label, NavKind kind, LinkTarget? target, int sortSeq, bool isEnabled = true, bool showAges = false)
    {
        Key = (key ?? string.Empty).Trim();
        Label = (label ?? string.Empty).Trim();
        Kind = kind;
        Target = target;
        SortSeq = sortSeq;
        IsEnabled = isEnabled;
        ShowAges = showAges;
    }

    public string Key { get; private set; }

    public string Label { get; private set; }

    public NavKind Kind { get; private set; }

    public LinkTarget? Target { get; private set; }

    public int SortSeq { get; private set; }

    public bool IsEnabled { get; private set; }

    /// <summary>Whether the mega-menu shows the "Shop by age" column for this entry (SCRUM-205 contract addition).</summary>
    public bool ShowAges { get; private set; }

    public IReadOnlyList<NavItem> Children => _children;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public NavItem AddChild(NavItem child)
    {
        _children.Add(child);
        return this;
    }
}
