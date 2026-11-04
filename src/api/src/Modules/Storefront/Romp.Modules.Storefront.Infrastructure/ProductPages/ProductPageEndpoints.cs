using System.Diagnostics.Metrics;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Application.ProductPages;
using Romp.Modules.Storefront.Domain.ProductPages;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.ProductPages;

/// <summary>Counts products Catalog has published that the read model does not hold yet, so a slow projection is visible (plan).</summary>
public sealed class ProductPageMetrics : IProductPageMetrics
{
    private static readonly Meter Meter = new("Romp.Storefront");
    private static readonly Counter<long> NotProjected = Meter.CreateCounter<long>("storefront.product_page.not_projected");

    public void NotProjectedYet() => NotProjected.Add(1);
}

/// <summary>
/// The anonymous product page read (PDP-1..17). All four outcomes answer 200 with one JSON shape so the server-rendering layer, the only
/// caller, sets 301, 404 or 410 itself. The redirect target comes from Catalog and nothing the visitor typed is echoed (PDP-13).
/// </summary>
public static class ProductPageEndpoints
{
    private const int MaxCodeLength = 40;

    public static void MapPublicProductPageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup(StorefrontSettingsEndpoints.PublicPrefix + "/products", Policies.Public).WithTags("Storefront");

        group.MapGet("/by-path", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var query = http.Request.Query;
            var path = query.TryGetValue("path", out var p) && p.Count > 0 ? p[0] : null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["path"] = ["A path is required."] });
            }

            if (path.Length > ProductPath.MaxLength)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["path"] = [$"A path is at most {ProductPath.MaxLength} characters."] });
            }

            var result = await sender.Send(new GetProductPageQuery(path, Code(query, "colour"), Code(query, "size")), cancellationToken);
            http.Response.Headers[HeaderNames.CacheControl] = result.Outcome == nameof(ProductPageOutcome.NotFound) ? "no-store" : "public, max-age=60";
            return Results.Ok(result);
        });

        MapProductCards(group);
    }

    /// <summary>
    /// Cards for the products a signed-out shopper has saved (their browser sends the ids). Public facts only; unpublished ids are left out.
    /// Anything that is not a list of at most 50 positive whole numbers is refused.
    /// </summary>
    internal static void MapProductCards(RouteGroupBuilder group) =>
        group.MapGet("/summaries", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var raw = http.Request.Query.TryGetValue("ids", out var v) && v.Count > 0 ? v[0] ?? string.Empty : string.Empty;
            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > GetProductCardsQuery.MaxIds || parts.Any(p => !long.TryParse(p, out var id) || id <= 0))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["ids"] = [$"Send up to {GetProductCardsQuery.MaxIds} product ids, separated by commas."] });
            }

            var cards = await sender.Send(new GetProductCardsQuery(parts.Select(long.Parse).ToList()), cancellationToken);
            http.Response.Headers[HeaderNames.CacheControl] = "public, max-age=60";
            return Results.Ok(cards);
        });

    /// <summary>A colour or size code from the query, or null when absent or too long to be one (it is then ignored, PDP-2).</summary>
    private static string? Code(IQueryCollection query, string key) =>
        query.TryGetValue(key, out var v) && v.Count > 0 && v[0] is { Length: > 0 and <= MaxCodeLength } code ? code : null;
}
