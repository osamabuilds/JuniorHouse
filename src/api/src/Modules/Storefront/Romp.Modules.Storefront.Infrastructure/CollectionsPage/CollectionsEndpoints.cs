using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Storefront.Application.CollectionsPage;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.CollectionsPage;

/// <summary>
/// The anonymous collections index (collections-page, COL-1..3, 6). Page and page size are read as text and fall back to their defaults when
/// they are not whole numbers, and the handler clamps them, so a hostile query string is never an error.
/// </summary>
public static class CollectionsEndpoints
{
    public static void MapPublicCollectionsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup(StorefrontSettingsEndpoints.PublicPrefix, Policies.Public).WithTags("Storefront");

        group.MapGet("/collections", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            http.Response.Headers[HeaderNames.CacheControl] = "public, max-age=60";
            var page = Number(http.Request.Query["page"].ToString(), 1);
            var pageSize = Number(http.Request.Query["pageSize"].ToString(), CollectionsPaging.DefaultPageSize);
            return Results.Ok(await sender.Send(new GetCollectionsQuery(page, pageSize), cancellationToken));
        });
    }

    private static int Number(string text, int fallback) =>
        text.Length is > 0 and <= 9 && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : fallback;
}
