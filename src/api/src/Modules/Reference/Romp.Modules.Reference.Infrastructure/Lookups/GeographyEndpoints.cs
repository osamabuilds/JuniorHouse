using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Reference.Infrastructure.Lookups;

/// <summary>
/// The province and city lists the address form fills its dependent selects from (customers/addresses ADD-4). Reference data, so anonymous
/// and shared-cacheable; nothing the caller sends is echoed.
/// </summary>
internal static class GeographyEndpoints
{
    private const string CacheControl = "public, max-age=3600";

    public static void MapGeographyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/geography", Policies.Public).WithTags("Reference");

        group.MapGet("/provinces", async (HttpContext http, IGeographyQueries queries, CancellationToken cancellationToken) =>
        {
            http.Response.Headers.CacheControl = CacheControl;
            return Results.Ok(await queries.GetProvincesAsync(cancellationToken));
        });

        group.MapGet("/provinces/{id:int}/cities", async (int id, HttpContext http, IGeographyQueries queries, CancellationToken cancellationToken) =>
        {
            var cities = id is > 0 and <= short.MaxValue ? await queries.GetCitiesAsync((short)id, cancellationToken) : null;
            if (cities is null)
            {
                return Results.NotFound();
            }

            http.Response.Headers.CacheControl = CacheControl;
            return Results.Ok(cities);
        });
    }
}
