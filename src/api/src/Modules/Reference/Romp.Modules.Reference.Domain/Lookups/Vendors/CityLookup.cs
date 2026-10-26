namespace Romp.Modules.Reference.Domain.Lookups.Vendors;

/// <summary>
/// Maps to REF.CITY_LKP. <see cref="ProvinceId"/> is null for a city staff added before assigning a province; such a city is not offered
/// to customers (an address needs a city in its province, ADD-4).
/// </summary>
public sealed class CityLookup : Lookup
{
    private CityLookup()
    {
    }

    public CityLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }

    public short? ProvinceId { get; private set; }

    public void AssignProvince(short provinceId) => ProvinceId = provinceId;
}
