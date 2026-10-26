using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Warehouse.Application.Bins;

namespace Romp.Modules.Warehouse.Infrastructure.Bins;

/// <summary>Body of POST /api/warehouse/bins.</summary>
public sealed record CreateBinRequest(string Zone, string Aisle, string Shelf, string ActingPerson);

/// <summary>Body of POST /api/warehouse/bins/{id}/active.</summary>
public sealed record SetBinActiveRequest(bool Active, string ActingPerson);

// Mapped through MapSecuredGroup (ADR 0011).
internal static class BinEndpoints
{
    private const long MaxBodyBytes = 4 * 1024;

    public static void MapBinEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/warehouse", Permissions.WarehouseRead).WithTags("Warehouse")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/bins", async (
                string? zone,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchBinsQuery(zone, page, pageSize), cancellationToken)));

        // The scan lookup: a bin code printed on the shelf (NFR-SC-01: one indexed query).
        group.MapGet("/bins/by-code/{code}", async (string code, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetBinByCodeQuery(code), cancellationToken) is { } bin ? Results.Ok(bin) : Results.NotFound());

        group.MapPost("/bins", async (CreateBinRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new CreateBinCommand(body.Zone, body.Aisle, body.Shelf, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.WarehouseWrite)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapPost("/bins/{id:long}/active", async (long id, SetBinActiveRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SetBinActiveCommand(id, body.Active, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.WarehouseWrite);
    }
}
