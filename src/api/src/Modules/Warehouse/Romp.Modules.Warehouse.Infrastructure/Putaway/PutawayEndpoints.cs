using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Warehouse.Application.Putaway;

namespace Romp.Modules.Warehouse.Infrastructure.Putaway;

/// <summary>
/// Body of POST /api/warehouse/putaway/items/{id}/confirm. <c>Scanned</c> is true when the GTIN was scanned and false when the item was
/// chosen from the list because no tag exists yet (PUT-19). The idempotency key makes a double submit apply once (PUT-8).
/// </summary>
public sealed record ConfirmPutawayRequest(string BinCode, int Quantity, bool Scanned, string? Gtin, Guid IdempotencyKey, string ActingPerson);

// Mapped through MapSecuredGroup (ADR 0011). The scan lookup is the hot path: GET, two indexed queries, no body.
internal static class PutawayEndpoints
{
    private const long MaxBodyBytes = 4 * 1024;

    public static void MapPutawayEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/warehouse/putaway").WithTags("Warehouse")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        group.MapGet("/items", async (
                short? statusId,
                bool? onlyOpen,
                string? search,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchPutawayItemsQuery(statusId, onlyOpen ?? false, search, page, pageSize), cancellationToken)));

        group.MapGet("/scan/{gtin}", async (string gtin, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new ScanLookupQuery(gtin), cancellationToken) is { } found ? Results.Ok(found) : Results.NotFound());

        group.MapPost("/items/{id:long}/confirm", async (long id, ConfirmPutawayRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new ConfirmPutawayCommand(id, body.BinCode, body.Quantity, body.Scanned, body.Gtin, body.IdempotencyKey, body.ActingPerson),
                    cancellationToken)))
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapGet("/records", async (
                long? itemId,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchPutawayRecordsQuery(itemId, page, pageSize), cancellationToken)));
    }
}
