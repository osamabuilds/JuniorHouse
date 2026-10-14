using System.Text;

namespace Romp.Modules.Labelling.Domain.Tags;

/// <summary>
/// Makes staff-entered text safe and short enough for a tag (TAG-7): control characters go, whitespace collapses, and long
/// text is cut with "..." without splitting a surrogate pair. Escaping for a specific language (ZPL) is a separate step.
/// </summary>
public static class TextSanitizer
{
    public static string Clean(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || maxLength <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var lastWasSpace = true;
        foreach (var c in text)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            builder.Append(c);
            lastWasSpace = false;
        }

        var cleaned = builder.ToString().TrimEnd();
        if (cleaned.Length <= maxLength)
        {
            return cleaned;
        }

        var cut = Math.Max(0, maxLength - 3);
        if (cut > 0 && char.IsLowSurrogate(cleaned[cut]))
        {
            cut--;
        }

        return cleaned[..cut].TrimEnd() + "...";
    }
}
