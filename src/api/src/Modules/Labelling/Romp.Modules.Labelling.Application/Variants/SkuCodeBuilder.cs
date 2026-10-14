using System.Text;

namespace Romp.Modules.Labelling.Application.Variants;

/// <summary>
/// Builds the readable SKU code <c>ROMP-{STYL_CODE}-{CLR_CODE}-{SIZE_CODE}</c> (D3). Each part is upper-cased and reduced to
/// letters, digits and single hyphens, so only safe characters can ever reach a tag, a file or a log. Uniqueness is inherited
/// from the unique style code; the database's unique index on the SKU is the final guard.
/// </summary>
public static class SkuCodeBuilder
{
    public const string Brand = "ROMP";

    public static string Build(string styleCode, string colourCode, string sizeCode) =>
        $"{Brand}-{Part(styleCode)}-{Part(colourCode)}-{Part(sizeCode)}";

    internal static string Part(string? code)
    {
        var builder = new StringBuilder();
        var lastWasHyphen = true;
        foreach (var c in (code ?? string.Empty).Trim().ToUpperInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        var part = builder.ToString().Trim('-');
        return part.Length == 0 ? "NA" : part;
    }
}
