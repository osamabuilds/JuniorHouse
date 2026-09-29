using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Catalog.Domain.Styles;

/// <summary>
/// Maps to CTLG.STYL_MAIN - the pre-production style/collection record (§5.9 step 1; no BRD FR id,
/// see spec.md's Open questions). Aggregate root for <see cref="StyleColourway"/>/
/// <see cref="StyleSize"/>/<see cref="StyleTargetLine"/> (AC-3, AC-4).
/// </summary>
public sealed class Style : AggregateRoot<long>, IAuditable
{
    private readonly List<StyleColourway> _colourways = [];
    private readonly List<StyleSize> _sizes = [];
    private readonly List<StyleTargetLine> _targetLines = [];

    private Style()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public Style(
        string code,
        string name,
        string? collectionName,
        short categoryId,
        short genderId,
        short ageBracketId,
        short fabricId,
        decimal targetUnitCost,
        decimal targetRetailPrice)
    {
        Code = code;
        Name = name;
        CollectionName = collectionName;
        CategoryId = categoryId;
        GenderId = genderId;
        AgeBracketId = ageBracketId;
        FabricId = fabricId;
        TargetUnitCost = targetUnitCost;
        TargetRetailPrice = targetRetailPrice;
        IsActive = true;
    }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? CollectionName { get; private set; }

    public short CategoryId { get; private set; }

    public short GenderId { get; private set; }

    public short AgeBracketId { get; private set; }

    public short FabricId { get; private set; }

    public decimal TargetUnitCost { get; private set; }

    public decimal TargetRetailPrice { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<StyleColourway> Colourways => _colourways.AsReadOnly();

    public IReadOnlyCollection<StyleSize> Sizes => _sizes.AsReadOnly();

    public IReadOnlyCollection<StyleTargetLine> TargetLines => _targetLines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public void UpdateDetails(
        string name,
        string? collectionName,
        short categoryId,
        short genderId,
        short ageBracketId,
        short fabricId,
        decimal targetUnitCost,
        decimal targetRetailPrice)
    {
        Name = name;
        CollectionName = collectionName;
        CategoryId = categoryId;
        GenderId = genderId;
        AgeBracketId = ageBracketId;
        FabricId = fabricId;
        TargetUnitCost = targetUnitCost;
        TargetRetailPrice = targetRetailPrice;
    }

    /// <summary>
    /// Replaces the style's colourways, size run and target-quantity grid together, since a
    /// target line is only valid if its size and colour are both present in the other two
    /// (spec AC-8's PO-line rule; the "considered a single table" risk note in plan.md applies
    /// the same invariant here). The caller (CreateStyleCommandHandler/UpdateStyleCommandHandler)
    /// is expected to have already turned an invalid combination into a field-level
    /// ValidationException before calling this - this guard is a last-resort invariant, not the
    /// primary validation path (AC-15 needs the error to come from the MediatR pipeline).
    /// </summary>
    public void SetColourSizeAndTargets(
        IEnumerable<short> colourIds,
        IEnumerable<short> sizeIds,
        IEnumerable<(short SizeId, short ColourId, int TargetQty)> targetLines)
    {
        var colours = colourIds.Distinct().ToList();
        var sizes = sizeIds.Distinct().ToList();
        var lines = targetLines.ToList();

        foreach (var line in lines)
        {
            if (!sizes.Contains(line.SizeId) || !colours.Contains(line.ColourId))
            {
                throw new InvalidOperationException(
                    $"Target line (size {line.SizeId}, colour {line.ColourId}) is not in this style's size run/colourways.");
            }
        }

        _colourways.Clear();
        _colourways.AddRange(colours.Select(colourId => new StyleColourway(Id, colourId)));

        _sizes.Clear();
        _sizes.AddRange(sizes.Select(sizeId => new StyleSize(Id, sizeId)));

        _targetLines.Clear();
        _targetLines.AddRange(lines.Select(line => new StyleTargetLine(Id, line.SizeId, line.ColourId, line.TargetQty)));
    }

    public void Retire() => IsActive = false;
}
