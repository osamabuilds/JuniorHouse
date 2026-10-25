using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Storefront.Application.Search;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.Search;

/// <summary>
/// The anonymous search reads (FR-04, FR-06). The typed text is only ever a parameter: it is normalised and capped by the domain, so an
/// over-long or hostile value is answered like any other text (SRC-3), and only malformed numbers are a 400. An empty result is a normal
/// 200 with no tiles (SRC-2); only a page past the last one is a 404.
/// </summary>
public static class SearchEndpoints
{
    public static void MapPublicSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup(StorefrontSettingsEndpoints.PublicPrefix + "/search", Policies.Public).WithTags("Storefront");

        group.MapGet("", async (HttpContext http, ISender sender, ISearchMissSink misses, CancellationToken cancellationToken) =>
        {
            var query = http.Request.Query;
            string? One(string key) => query.TryGetValue(key, out var v) && v.Count > 0 ? v[0] : null;

            var (filter, errors) = ListingFilter.Parse(query["size"], query["colour"], One("age"), One("price"), One("sort"), One("page"), One("pageSize"));
            if (filter is null)
            {
                return Results.ValidationProblem(errors.GroupBy(e => e.Field).ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray()));
            }

            var text = One("q");
            var result = await sender.Send(new SearchProductsQuery(text, filter), cancellationToken);
            switch (result.Outcome)
            {
                case SearchOutcome.PastTheEnd:
                    return Results.Problem(title: "Not found.", statusCode: StatusCodes.Status404NotFound);
                case SearchOutcome.NoResults:
                    // Only a search the filters did not narrow counts as a miss: the words themselves found nothing (SRC-13).
                    if (!filter.HasAny && filter.Page == 1)
                    {
                        misses.Offer(text);
                    }

                    http.Response.Headers[HeaderNames.CacheControl] = "no-store";
                    return Results.Ok(result.Page);
                default:
                    http.Response.Headers[HeaderNames.CacheControl] = "public, max-age=60";
                    return Results.Ok(result.Page);
            }
        });

        group.MapGet("/suggest", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            http.Response.Headers[HeaderNames.CacheControl] = "public, max-age=30";
            return Results.Ok(await sender.Send(new SuggestQuery(Query(http, "q")), cancellationToken));
        });

        group.MapGet("/sku", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var target = await sender.Send(new LookupSkuQuery(Query(http, "code")), cancellationToken);
            if (target is null)
            {
                return Results.Problem(title: "Not found.", statusCode: StatusCodes.Status404NotFound);
            }

            http.Response.Headers[HeaderNames.CacheControl] = "public, max-age=60";
            return Results.Ok(target);
        });
    }

    private static string? Query(HttpContext http, string key) =>
        http.Request.Query.TryGetValue(key, out var v) && v.Count > 0 ? v[0] : null;
}
