using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Catalog.Application.Styles;

namespace Romp.Modules.Catalog.Infrastructure.Styles;

internal static class StyleEndpoints
{
    public static void MapStyleEndpoints(this IEndpointRouteBuilder endpoints, string tag)
    {
        var group = endpoints.MapGroup("/api/catalog/styles").WithTags(tag);

        group.MapPost("/", async (CreateStyleCommand command, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(command, cancellationToken)));

        group.MapPut("/{id:long}", async (
                long id,
                UpdateStyleRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body.ToCommand(id), cancellationToken)));

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
                bool activeOnly = true) =>
            Results.Ok(await sender.Send(new SearchStylesQuery(search, categoryId, activeOnly), cancellationToken)));
    }
}
