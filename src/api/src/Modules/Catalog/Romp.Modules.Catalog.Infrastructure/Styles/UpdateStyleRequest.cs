using Romp.Modules.Catalog.Application.Styles;

namespace Romp.Modules.Catalog.Infrastructure.Styles;

/// <summary>PUT request body for a style update - the id comes from the route, not the body.</summary>
public sealed record UpdateStyleRequest(
    string Name,
    string? CollectionName,
    short CategoryId,
    short GenderId,
    short AgeBracketId,
    short FabricId,
    decimal TargetUnitCost,
    decimal TargetRetailPrice,
    IReadOnlyCollection<short> ColourIds,
    IReadOnlyCollection<short> SizeIds,
    IReadOnlyCollection<StyleTargetLineDto> TargetLines)
{
    public UpdateStyleCommand ToCommand(long id) => new(
        id, Name, CollectionName, CategoryId, GenderId, AgeBracketId, FabricId,
        TargetUnitCost, TargetRetailPrice, ColourIds, SizeIds, TargetLines);
}
