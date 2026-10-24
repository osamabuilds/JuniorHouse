using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Storefront.Domain.StorefrontSettings;

/// <summary>One cell of a list (column 0) or table block, in reading order. Maps to STFR.PAGE_BLCK_ITM.</summary>
public sealed class PageBlockItem : Entity<long>, IAuditable
{
    public PageBlockItem(int rowNo, int columnNo, string text)
    {
        RowNo = rowNo;
        ColumnNo = columnNo;
        Text = text ?? string.Empty;
    }

    private PageBlockItem() => Text = string.Empty;

    public int RowNo { get; private set; }

    public int ColumnNo { get; private set; }

    public string Text { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// A structured block of a page. <see cref="Text"/> is plain text (heading, paragraph, callout); <see cref="Level"/> is 2 or 3 for a
/// heading; a fact block names a fact key and may carry a label. Maps to STFR.PAGE_BLCK.
/// </summary>
public sealed class PageBlock : Entity<long>, IAuditable
{
    private readonly List<PageBlockItem> _items = [];

    private PageBlock() => Text = string.Empty;

    public PageBlock(BlockType type, string text = "", int level = 0, string? factKey = null, string? label = null, IEnumerable<PageBlockItem>? items = null)
    {
        Type = type;
        Text = text ?? string.Empty;
        Level = level;
        FactKey = factKey;
        Label = label;
        if (items is not null)
        {
            _items.AddRange(items);
        }
    }

    public BlockType Type { get; private set; }

    public string Text { get; private set; }

    public int Level { get; private set; }

    public string? FactKey { get; private set; }

    public string? Label { get; private set; }

    public IReadOnlyList<PageBlockItem> Items => _items;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
