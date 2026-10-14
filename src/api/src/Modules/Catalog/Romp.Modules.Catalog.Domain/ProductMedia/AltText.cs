using System.Text.RegularExpressions;

namespace Romp.Modules.Catalog.Domain.ProductMedia;

/// <summary>
/// The rules for an image's alt text (SEO-07, MED-2, MED-3) and a suggestion staff can edit. Alt text is plain text: it is stored as
/// typed and escaped by the page that renders it (MED-4), so a script in it is harmless but pointless, and is refused as noise.
/// </summary>
public static partial class AltText
{
    public const int MaxLength = 125;

    private const int MaxRepeatsOfOneWord = 3;

    // IMG_1234.jpg, DSC00012.JPG, photo.png, 20240101_120000.jpeg: a file name is not a description.
    [GeneratedRegex(@"\.(jpe?g|png|webp|gif|bmp|tiff?|heic|svg)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageExtension();

    [GeneratedRegex(@"^(img|dsc|dscn|pxl|image|photo|pic|screenshot)[\s_\-]*\d+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CameraName();

    [GeneratedRegex(@"[<>]", RegexOptions.CultureInvariant)]
    private static partial Regex MarkupCharacters();

    /// <summary>The first problem with the text, or null when it is acceptable.</summary>
    public static string? Validate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Describe what the image shows.";
        }

        var trimmed = text.Trim();
        if (trimmed.Length > MaxLength)
        {
            return $"Alt text can be at most {MaxLength} characters (it is {trimmed.Length}).";
        }

        if (ImageExtension().IsMatch(trimmed) || CameraName().IsMatch(trimmed))
        {
            return "Alt text should describe the image, not repeat its file name.";
        }

        if (MarkupCharacters().IsMatch(trimmed))
        {
            return "Alt text is plain text: remove the < and > characters.";
        }

        if (!trimmed.Any(char.IsLetter))
        {
            return "Alt text needs words, not only digits or punctuation.";
        }

        var words = trimmed.Split([' ', ',', ';', '.', '-', '_', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length > 2);
        if (words.GroupBy(w => w).Any(g => g.Count() > MaxRepeatsOfOneWord))
        {
            return "Alt text repeats the same word too often. Describe the image in a natural sentence.";
        }

        return null;
    }

    /// <summary>A starting point staff edit: "{product} in {colour}, view {n}" (n counts from 1).</summary>
    public static string Suggest(string productName, string? colourName, int viewNumber)
    {
        var name = productName.Trim();
        var text = string.IsNullOrWhiteSpace(colourName) ? $"{name}, view {viewNumber}" : $"{name} in {colourName.Trim()}, view {viewNumber}";
        return text.Length <= MaxLength ? text : text[..MaxLength].TrimEnd();
    }
}
