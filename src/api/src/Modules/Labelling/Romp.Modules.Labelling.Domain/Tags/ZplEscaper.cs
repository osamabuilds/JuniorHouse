using System.Text;

namespace Romp.Modules.Labelling.Domain.Tags;

/// <summary>
/// Makes text safe to put inside a ZPL field (TAG-6). Staff-entered product names reach the printer, so no input may be able to
/// add, change or end a command: every character outside a small safe set is written as the field-hex escape <c>_XX</c> per UTF-8
/// byte (the field is opened with <c>^FH_</c> and the format selects UTF-8 with <c>^CI28</c>). The command characters
/// <c>^</c> and <c>~</c>, the hex indicator <c>_</c> and the backslash are therefore never present in the output unescaped.
/// </summary>
public static class ZplEscaper
{
    private const string SafePunctuation = " .,-/():+&'!?%@#*=;\"<>[]{}|";

    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        Span<byte> bytes = stackalloc byte[4];
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.IsAscii && (char.IsAsciiLetterOrDigit((char)rune.Value) || SafePunctuation.Contains((char)rune.Value)))
            {
                builder.Append((char)rune.Value);
                continue;
            }

            var length = rune.EncodeToUtf8(bytes);
            for (var i = 0; i < length; i++)
            {
                builder.Append('_').Append(bytes[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }
}
