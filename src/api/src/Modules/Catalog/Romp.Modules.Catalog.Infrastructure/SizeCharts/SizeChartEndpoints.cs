using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Catalog.Application.SizeCharts;

namespace Romp.Modules.Catalog.Infrastructure.SizeCharts;

/// <summary>Body of POST /api/catalog/size-charts.</summary>
public sealed record CreateSizeChartRequest(short CategoryId, short GenderId, string Name, IReadOnlyList<SizeChartLineInput> Lines, string ActingPerson);

/// <summary>Body of PUT /api/catalog/size-charts/{id}.</summary>
public sealed record UpdateSizeChartRequest(string Name, IReadOnlyList<SizeChartLineInput> Lines, uint Version, string ActingPerson);

// Mapped through MapSecuredGroup with the Catalogue policy (ADR 0011, SEC-08).
internal static class SizeChartEndpoints
{
    private const long MaxBodyBytes = 64 * 1024;

    public static void MapSizeChartEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/catalog", Permissions.CatalogRead).WithTags("Catalog")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/size-charts", async (ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListSizeChartsQuery(page, pageSize), cancellationToken)));

        group.MapGet("/size-charts/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetSizeChartQuery(id), cancellationToken)));

        group.MapPost("/size-charts", async (CreateSizeChartRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new CreateSizeChartCommand(body.CategoryId, body.GenderId, body.Name, body.Lines, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapPut("/size-charts/{id:long}", async (long id, UpdateSizeChartRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new UpdateSizeChartCommand(id, body.Name, body.Lines, body.Version, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.CatalogWrite);

        group.MapDelete("/size-charts/{id:long}", async (long id, [FromQuery] string actingPerson, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteSizeChartCommand(id, actingPerson), cancellationToken);
            return Results.NoContent();
        }).RequirePermission(Permissions.CatalogWrite);

        group.MapGet("/products/{id:long}/size-chart", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetChartForProductQuery(id), cancellationToken)));
    }
}
