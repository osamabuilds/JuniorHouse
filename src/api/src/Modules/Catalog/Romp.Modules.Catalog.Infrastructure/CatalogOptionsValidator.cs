using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Romp.Modules.Catalog.Application.Abstractions;

namespace Romp.Modules.Catalog.Infrastructure;

/// <summary>
/// Start-up validation of <c>Catalog:*</c> (plan G7): a limit outside its range, an unknown image type or a reserved slug that is not
/// itself a valid slug stops the host from starting, instead of failing on the first upload.
/// </summary>
public sealed partial class CatalogOptionsValidator : IValidateOptions<CatalogOptions>
{
    private const int MaxImageBytesCeiling = 25 * 1024 * 1024;
    private const int MaxImagesCeiling = 50;

    private static readonly string[] KnownImageTypes = ["jpeg", "png", "webp"];

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public ValidateOptionsResult Validate(string? name, CatalogOptions options)
    {
        var failures = new List<string>();

        if (options.MaxImageBytes is < 1024 or > MaxImageBytesCeiling)
        {
            failures.Add($"Catalog:MaxImageBytes must be between 1024 and {MaxImageBytesCeiling}.");
        }

        if (options.MaxImagesPerProduct is < 1 or > MaxImagesCeiling)
        {
            failures.Add($"Catalog:MaxImagesPerProduct must be between 1 and {MaxImagesCeiling}.");
        }

        if (options.AllowedImageTypes is null || options.AllowedImageTypes.Length == 0 || options.AllowedImageTypes.Any(t => !KnownImageTypes.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            failures.Add("Catalog:AllowedImageTypes must list at least one of jpeg, png, webp.");
        }

        if (options.MaxSlugLength is < 10 or > 80)
        {
            failures.Add("Catalog:MaxSlugLength must be between 10 and 80 (the column holds 80).");
        }

        if (options.ReservedSlugs is null || options.ReservedSlugs.Any(s => !SlugPattern().IsMatch(s)))
        {
            failures.Add("Catalog:ReservedSlugs must be a list of lower-case slugs (letters, digits and single hyphens).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
