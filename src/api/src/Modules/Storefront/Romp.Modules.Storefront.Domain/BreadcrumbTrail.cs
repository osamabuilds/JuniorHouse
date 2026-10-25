namespace Romp.Modules.Storefront.Domain;

/// <summary>One step of a breadcrumb trail. The current page has no <see cref="Path"/>: it is shown but not linked (NAV-4).</summary>
public sealed record Crumb(string Label, string? Path);

/// <summary>
/// The trail shown on a page below the home page, and the list the <c>BreadcrumbList</c> structured data is built from, so the two agree
/// (NAV-4, SEO-03). Home, then audience, then category, then product; the last step is the current page.
/// </summary>
public static class BreadcrumbTrail
{
    public const string HomeLabel = "Home";

    /// <summary>The trail for a route. Pass the pieces that exist, outermost first; null stops the trail. The home page has no trail.</summary>
    public static IReadOnlyList<Crumb> For(MenuAudienceRef? audience, MenuCategoryRef? category, string? productName)
    {
        var steps = new List<(string Label, string Path)> { (HomeLabel, "/") };
        if (audience is not null)
        {
            steps.Add((audience.Name, "/" + CodeSegment.Of(audience.Code)));
            if (category is not null)
            {
                steps.Add((category.Name, $"/{CodeSegment.Of(audience.Code)}/{CodeSegment.Of(category.Code)}"));
            }
        }

        if (!string.IsNullOrWhiteSpace(productName))
        {
            steps.Add((productName.Trim(), string.Empty));
        }

        if (steps.Count == 1)
        {
            return [];
        }

        return steps.Select((s, i) => new Crumb(s.Label, i == steps.Count - 1 ? null : s.Path)).ToList();
    }

    /// <summary>The trail for a listing that is not tied to an audience or category (New In, Sale, a collection).</summary>
    public static IReadOnlyList<Crumb> ForPage(string label) =>
        [new Crumb(HomeLabel, "/"), new Crumb(label.Trim(), null)];
}

public sealed record MenuAudienceRef(string Code, string Name);

public sealed record MenuCategoryRef(string Code, string Name);
