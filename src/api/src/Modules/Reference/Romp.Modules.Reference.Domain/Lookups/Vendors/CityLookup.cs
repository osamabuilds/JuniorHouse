namespace Romp.Modules.Reference.Domain.Lookups.Vendors;

/// <summary>Maps to REF.CITY_LKP.</summary>
public sealed class CityLookup : Lookup
{
    private CityLookup()
    {
    }

    public CityLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
