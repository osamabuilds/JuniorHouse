namespace Romp.Modules.Reference.Domain.Lookups.Apparel;

/// <summary>
/// Maps to REF.CARE_LKP (Sprint 7, Catalog). <see cref="SymbolCode"/> is the key of the ISO 3758 care symbol the storefront draws
/// (owner decision D6). System-owned: the symbol pairing is structural, so rows are added by migration, not through the admin screen.
/// </summary>
public sealed class CareInstructionLookup : Lookup
{
    private CareInstructionLookup()
    {
        SymbolCode = string.Empty;
    }

    public CareInstructionLookup(string code, string name, string? description, short sortSeq, string symbolCode)
        : base(code, name, description, sortSeq)
    {
        SymbolCode = symbolCode;
    }

    public string SymbolCode { get; private set; }
}
