namespace Romp.Modules.Reference.Application.Lookups;

/// <summary>
/// Shared read shape for every REF lookup (AC-1, AC-2). <see cref="ParentCategoryId"/> and
/// <see cref="DefaultAdvancePercent"/> are populated only for <c>CategoryLookup</c>/
/// <c>PaymentTermLookup</c> respectively and are null for every other lookup type.
/// </summary>
public sealed record LookupDto(
    short Id,
    string Code,
    string Name,
    string? Description,
    short SortSeq,
    bool IsActive,
    short? ParentCategoryId,
    decimal? DefaultAdvancePercent);
