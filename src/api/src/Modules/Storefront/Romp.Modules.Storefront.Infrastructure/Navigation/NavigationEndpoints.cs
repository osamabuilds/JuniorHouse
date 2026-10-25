using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Storefront.Application.Navigation;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.Navigation;

/// <summary>The anonymous navigation reads (NAV-1..9). No request input: the menu is the same for every visitor.</summary>
public static class NavigationEndpoints
{
    public static void MapPublicNavigationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup(StorefrontSettingsEndpoints.PublicPrefix, Policies.Public).WithTags("Storefront");

        group.MapGet("/menu", async (HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            http.Response.Headers[HeaderNames.CacheControl] = "public, max-age=60";
            return Results.Ok(await sender.Send(new GetMenuQuery(), cancellationToken));
        });
    }
}
