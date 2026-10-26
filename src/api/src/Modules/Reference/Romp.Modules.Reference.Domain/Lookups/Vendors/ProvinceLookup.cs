namespace Romp.Modules.Reference.Domain.Lookups.Vendors;

/// <summary>Maps to REF.PROV_LKP: a province or territory; cities belong to one (customers/addresses ADD-4).</summary>
public sealed class ProvinceLookup : Lookup
{
    private ProvinceLookup()
    {
    }

    public ProvinceLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
