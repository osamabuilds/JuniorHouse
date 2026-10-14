using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Domain.Tags;

/// <summary>
/// The EAN-13 bar pattern (TAG-2): 95 modules, 1 = bar, 0 = space. Pure and dependency-free, so it is tested against known
/// patterns. The first digit is not drawn; it selects the parity pattern of the left half.
/// </summary>
public static class Ean13
{
    public const int ModuleCount = 95;

    private static readonly string[] L =
        ["0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011"];

    private static readonly string[] G =
        ["0100111", "0110011", "0011011", "0100001", "0011101", "0111001", "0000101", "0010001", "0001001", "0010111"];

    private static readonly string[] R =
        ["1110010", "1100110", "1101100", "1000010", "1011100", "1001110", "1010000", "1000100", "1001000", "1110100"];

    private static readonly string[] Parity =
        ["LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"];

    /// <summary>The 95-module pattern for a valid GTIN-13; throws for anything else, so a corrupt barcode can never be drawn.</summary>
    public static string Encode(string gtin13)
    {
        var gtin = Gtin.Parse(gtin13);
        var digits = gtin.Value.Select(c => c - '0').ToArray();
        var parity = Parity[digits[0]];

        var pattern = new System.Text.StringBuilder(ModuleCount);
        pattern.Append("101");
        for (var i = 0; i < 6; i++)
        {
            pattern.Append(parity[i] == 'L' ? L[digits[i + 1]] : G[digits[i + 1]]);
        }

        pattern.Append("01010");
        for (var i = 7; i < 13; i++)
        {
            pattern.Append(R[digits[i]]);
        }

        pattern.Append("101");
        return pattern.Length == ModuleCount ? pattern.ToString() : throw new DomainException("EAN-13 encoding produced an unexpected length.");
    }
}
