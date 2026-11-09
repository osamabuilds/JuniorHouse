using System.Text;
using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Shipping.Application.Labels;

/// <summary>
/// Code 128 set B for a waybill (LBL-3): the pattern of bars and spaces a scanner reads. A pure function. The result is a string of <c>1</c> (bar) and
/// <c>0</c> (space) modules, quiet zone not included, which the label draws as an SVG.
/// </summary>
public static class Code128
{
    // The 107 patterns: six alternating bar and space widths for each symbol value (0 to 102, then start A, B, C); the stop symbol has seven.
    private static readonly string[] Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232",
    ];

    private const string Stop = "2331112";
    private const int StartB = 104;

    public static int PatternCount => Patterns.Length;

    public static string PatternOf(int value) => Patterns[value];

    /// <summary>The check symbol value for the text (start value plus each symbol times its position, modulo 103).</summary>
    public static int Checksum(string text)
    {
        var sum = StartB;
        for (var i = 0; i < text.Length; i++)
        {
            sum += (i + 1) * (text[i] - 32);
        }

        return sum % 103;
    }

    public static string Modules(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 40 || text.Any(c => c is < ' ' or > '~'))
        {
            throw new DomainException("A barcode text is 1 to 40 printable characters.");
        }

        var builder = new StringBuilder();
        Append(builder, Patterns[StartB]);
        foreach (var c in text)
        {
            Append(builder, Patterns[c - 32]);
        }

        Append(builder, Patterns[Checksum(text)]);
        Append(builder, Stop);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string widths)
    {
        var bar = true;
        foreach (var w in widths)
        {
            builder.Append(bar ? '1' : '0', w - '0');
            bar = !bar;
        }
    }
}
