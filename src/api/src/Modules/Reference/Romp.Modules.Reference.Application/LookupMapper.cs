using Romp.Modules.Reference.Domain;

namespace Romp.Modules.Reference.Application;

internal static class LookupMapper
{
    public static LookupDto ToDto(this Lookup lookup) => new(
        lookup.Id,
        lookup.Code,
        lookup.Name,
        lookup.Description,
        lookup.SortSeq,
        lookup.IsActive,
        (lookup as CategoryLookup)?.ParentCategoryId,
        (lookup as PaymentTermLookup)?.DefaultAdvancePercent);
}
