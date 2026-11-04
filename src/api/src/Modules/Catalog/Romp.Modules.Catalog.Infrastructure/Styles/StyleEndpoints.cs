using Romp.BuildingBlocks.Web;
using MediatR;
using Romp.BuildingBlocks.Application.Paging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Catalog.Application.Styles;

namespace Romp.Modules.Catalog.Infrastructure.Styles;

internal static class StyleEndpoints
{
    public static void MapStyleEndpoints(this IEndpointRouteBuilder endpoints, string tag)
    {
        var group = endpoints.MapSecuredGroup("/api/catalog/styles", Permissions.CatalogRead).WithTags(tag);

        group.MapPost("/", async (CreateStyleCommand command, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(command, cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapPut("/{id:long}", async (
                long id,
                UpdateStyleRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body.ToCommand(id), cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
        {
            var style = await sender.Send(new GetStyleByIdQuery(id), cancellationToken);
            return style is null ? Results.NotFound() : Results.Ok(style);
        });

        group.MapGet("/", async (
                string? search,
                short? categoryId,
                ISender sender,
                CancellationToken cancellationToken,
                bool activeOnly = true,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchStylesQuery(search, categoryId, activeOnly, page, pageSize), cancellationToken)));
    }
}
