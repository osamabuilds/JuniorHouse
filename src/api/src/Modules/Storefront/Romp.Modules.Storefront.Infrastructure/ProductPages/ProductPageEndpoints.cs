using System.Diagnostics.Metrics;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Romp.BuildingBlocks.Web;
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
    }

    /// <summary>A colour or size code from the query, or null when absent or too long to be one (it is then ignored, PDP-2).</summary>
    private static string? Code(IQueryCollection query, string key) =>
        query.TryGetValue(key, out var v) && v.Count > 0 && v[0] is { Length: > 0 and <= MaxCodeLength } code ? code : null;
}
