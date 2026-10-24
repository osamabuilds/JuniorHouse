using System.Text.RegularExpressions;

namespace Romp.Modules.Storefront.Domain.StorefrontSettings;

/// <summary>
/// A navigation or footer link target: a kind plus a reference (a path, a page slug, a collection or category code, or an https URL on
/// the allow-list). Never a free <c>javascript:</c> URL (plan, abuse case 2).
/// </summary>
public sealed record LinkTarget(LinkTargetKind Kind, string Reference)
{
    private static readonly Regex PathPattern = new("^/([a-z0-9_-]+(/[a-z0-9_-]+)*)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CodePattern = new("^[a-z0-9-]{2,40}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The first problem with the target, or null when it is acceptable.</summary>
    public static string? Check(LinkTargetKind kind, string? reference, IReadOnlyCollection<string> allowedHosts)
    {
        var value = (reference ?? string.Empty).Trim();
        switch (kind)
        {
            case LinkTargetKind.Path:
                return PathPattern.IsMatch(value) ? null : "Enter a path that starts with / and uses lowercase letters, digits, hyphens and slashes, for example /girls/dresses.";
            case LinkTargetKind.Page:
                return Page.CheckSlug(value);
            case LinkTargetKind.Collection:
            case LinkTargetKind.Category:
                return CodePattern.IsMatch(value) ? null : "Enter a code of 2 to 40 lowercase letters, digits or hyphens.";
            case LinkTargetKind.External:
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
                {
                    return "Enter a full https:// address.";
                }

                return allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
                    ? null
                    : $"The site {uri.Host} is not on the list of allowed external sites.";
            default:
                return "Choose what the link points at.";
        }
    }
}
