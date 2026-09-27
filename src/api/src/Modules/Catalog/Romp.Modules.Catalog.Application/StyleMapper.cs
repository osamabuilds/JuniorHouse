using Romp.Modules.Catalog.Domain;

namespace Romp.Modules.Catalog.Application;

internal static class StyleMapper
{
    public static StyleDto ToDto(this Style style) => new(
        style.Id,
        style.Code,
        style.Name,
        style.CollectionName,
        style.CategoryId,
        style.GenderId,
        style.AgeBracketId,
        style.FabricId,
        style.TargetUnitCost,
        style.TargetRetailPrice,
        style.IsActive,
        style.Colourways.Select(c => c.ColourId).ToList(),
        style.Sizes.Select(s => s.SizeId).ToList(),
        style.TargetLines.Select(t => new StyleTargetLineDto(t.SizeId, t.ColourId, t.TargetQty)).ToList());

    public static StyleSummaryDto ToSummaryDto(this Style style) => new(
        style.Id,
        style.Code,
        style.Name,
        style.CategoryId,
        style.IsActive);
}
