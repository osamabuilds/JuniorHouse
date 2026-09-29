using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Quality.Application.Receiving;

namespace Romp.Modules.Quality.Infrastructure.Receiving;

/// <summary>Body of POST /api/quality/grns. The acting person is required on every write (AC-8, AC-30).</summary>
public sealed record RecordGoodsReceiptRequest(
    long InboundDeliveryId,
    DateOnly ReceivedDate,
    string ActingPerson,
    string? Note,
    IReadOnlyList<CountedLineDto> Lines);

// No authentication until Sprint 8 (SEC-08, SEC-05), so no role or ownership check applies yet. Tagged "Quality" for that sweep.
internal static class GoodsReceiptEndpoints
{
    public static void MapGoodsReceiptEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/quality/grns").WithTags("Quality");

        group.MapGet("/", async (
                string? search,
                short? reconciliationId,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchGoodsReceiptsQuery(search, reconciliationId, page, pageSize), cancellationToken)));

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetGoodsReceiptQuery(id), cancellationToken)));

        group.MapGet("/by-po/{poId:long}", async (long poId, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListGoodsReceiptsByPoQuery(poId), cancellationToken)));

        group.MapPost("/", async (RecordGoodsReceiptRequest body, ISender sender, CancellationToken cancellationToken) =>
        {
            var grn = await sender.Send(
                new RecordGoodsReceiptCommand(body.InboundDeliveryId, body.ReceivedDate, body.ActingPerson, body.Note, body.Lines),
                cancellationToken);
            return Results.Created($"/api/quality/grns/{grn.Id}", grn);
        });
    }
}
