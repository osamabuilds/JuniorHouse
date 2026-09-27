namespace Romp.Modules.Catalog.Application;

public sealed record StyleTargetLineDto(short SizeId, short ColourId, int TargetQty);

/// <summary>Full style detail (AC-3).</summary>
public sealed record StyleDto(
    long Id,
    string Code,
    string Name,
    string? CollectionName,
    short CategoryId,
    short GenderId,
    short AgeBracketId,
    short FabricId,
    decimal TargetUnitCost,
    decimal TargetRetailPrice,
    bool IsActive,
    IReadOnlyCollection<short> ColourIds,
    IReadOnlyCollection<short> SizeIds,
    IReadOnlyCollection<StyleTargetLineDto> TargetLines);

/// <summary>Row shape for the style search/list screen.</summary>
public sealed record StyleSummaryDto(
    long Id,
    string Code,
    string Name,
    short CategoryId,
    bool IsActive);
