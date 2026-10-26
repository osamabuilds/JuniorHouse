using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Warehouse.Application.Adjustments;

namespace Romp.Modules.Warehouse.Infrastructure.Adjustments;

/// <summary>
/// Body of POST /api/warehouse/adjustments. A reason and the acting person are required (NFR-SC-05). Above the large-adjustment limit
/// the first call is refused with "Confirmation required"; the client shows its confirmation step and resends with
/// <c>Reconfirmed</c> true (ADJ-4b). There is no way to send a balance or total (LED-10).
/// </summary>
public sealed record PostAdjustmentRequest(
    long? VariantRefId, string? Gtin, string BinCode, short GradeId, int QuantityChange, short ReasonId, string? Note, bool Reconfirmed,
    Guid IdempotencyKey, string ActingPerson);

// Reads in Staff; the write in the Inventory group (capability-shaped, enforced for real in S8, SEC-08). No PUT or DELETE exists: an
// adjustment is immutable and a mistake is corrected by a new, opposite one (ADJ-6).
internal static class AdjustmentEndpoints
{
    private const long MaxBodyBytes = 4 * 1024;

    public static void MapAdjustmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reads = endpoints.MapSecuredGroup("/api/warehouse/adjustments", Permissions.InventoryRead).WithTags("Warehouse");

        reads.MapGet("", async (
                short? reasonId,
                long? variantId,
                DateOnly? from,
                DateOnly? to,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchAdjustmentsQuery(reasonId, variantId, from, to, page, pageSize), cancellationToken)));

        reads.MapGet("/reasons", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListAdjustmentReasonsQuery(), cancellationToken)));

        endpoints.MapSecuredGroup("/api/warehouse/adjustments", Permissions.InventoryWrite).WithTags("Warehouse")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .MapPost("", async (PostAdjustmentRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new PostAdjustmentCommand(
                        body.VariantRefId, body.Gtin, body.BinCode, body.GradeId, body.QuantityChange, body.ReasonId, body.Note,
                        body.Reconfirmed, body.IdempotencyKey, body.ActingPerson),
                    cancellationToken))).RequirePermission(Permissions.InventoryWrite)
            .RequireRateLimiting(RateLimitPolicies.Strict);
    }
}
