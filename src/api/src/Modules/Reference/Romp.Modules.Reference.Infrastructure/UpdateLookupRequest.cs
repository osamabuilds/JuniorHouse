namespace Romp.Modules.Reference.Infrastructure;

/// <summary>PUT request body for a lookup update - the id comes from the route, not the body.</summary>
public sealed record UpdateLookupRequest(
    string Name,
    string? Description,
    short SortSeq,
    short? ParentCategoryId = null,
    decimal? DefaultAdvancePercent = null);
