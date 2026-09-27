using Romp.Modules.Reference.Domain;

namespace Romp.Modules.Reference.Application;

/// <summary>
/// Factory pattern: builds the right concrete <see cref="Lookup"/> subtype for a generic
/// <see cref="CreateLookupCommand{TLookup}"/> - needed because every lookup shares the same base
/// shape (code, name, description, sort order) except <c>CategoryLookup</c>/<c>PaymentTermLookup</c>,
/// which each take one extra constructor argument the other nine don't have. Centralised here so
/// the generic command handler doesn't need an 11-way type switch of its own.
/// </summary>
internal static class LookupFactory
{
    public static TLookup Create<TLookup>(
        string code,
        string name,
        string? description,
        short sortSeq,
        short? parentCategoryId,
        decimal? defaultAdvancePercent)
        where TLookup : Lookup
    {
        Lookup lookup = typeof(TLookup) switch
        {
            var t when t == typeof(CategoryLookup) => new CategoryLookup(code, name, description, sortSeq, parentCategoryId),
            var t when t == typeof(PaymentTermLookup) => new PaymentTermLookup(code, name, description, sortSeq, defaultAdvancePercent ?? 0m),
            var t when t == typeof(SizeLookup) => new SizeLookup(code, name, description, sortSeq),
            var t when t == typeof(ColourLookup) => new ColourLookup(code, name, description, sortSeq),
            var t when t == typeof(FabricLookup) => new FabricLookup(code, name, description, sortSeq),
            var t when t == typeof(GenderLookup) => new GenderLookup(code, name, description, sortSeq),
            var t when t == typeof(AgeBracketLookup) => new AgeBracketLookup(code, name, description, sortSeq),
            var t when t == typeof(CityLookup) => new CityLookup(code, name, description, sortSeq),
            var t when t == typeof(VendorSpecialisationLookup) => new VendorSpecialisationLookup(code, name, description, sortSeq),
            var t when t == typeof(PoCancelReasonLookup) => new PoCancelReasonLookup(code, name, description, sortSeq),
            var t when t == typeof(PoStatusLookup) => new PoStatusLookup(code, name, description, sortSeq),
            _ => throw new NotSupportedException($"{typeof(TLookup).Name} is not a known lookup type."),
        };

        return (TLookup)lookup;
    }

    /// <summary>Applies the extra field, if any, that <see cref="Lookup.Update"/> doesn't cover.</summary>
    public static void ApplyExtra(Lookup lookup, short? parentCategoryId, decimal? defaultAdvancePercent)
    {
        switch (lookup)
        {
            case CategoryLookup category:
                category.SetParent(parentCategoryId);
                break;
            case PaymentTermLookup paymentTerm when defaultAdvancePercent.HasValue:
                paymentTerm.SetDefaultAdvancePercent(defaultAdvancePercent.Value);
                break;
        }
    }
}
