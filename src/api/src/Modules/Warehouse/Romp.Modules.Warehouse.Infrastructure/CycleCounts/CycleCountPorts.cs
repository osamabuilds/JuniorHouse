using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Warehouse.Application.CycleCounts;
using Romp.Modules.Warehouse.Infrastructure.Persistence;

namespace Romp.Modules.Warehouse.Infrastructure.CycleCounts;

/// <summary>ADR 0006: <c>CC-{YYYY}-{NNNN}</c> from an atomic upsert on the row for the (Pakistan-time) year. Runs inside the caller's transaction; identifiers are quoted.</summary>
public sealed class CountNumberAllocator(WhseDbContext dbContext, TimeProvider timeProvider) : ICountNumberAllocator
{
    public async Task<string> AllocateAsync(CancellationToken cancellationToken)
    {
        var year = (short)timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(5)).Year;

        var rows = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                INSERT INTO "WHSE"."CNT_NO_SEQ" ("YR", "SEQ") VALUES ({0}, 1)
                ON CONFLICT ("YR") DO UPDATE SET "SEQ" = "WHSE"."CNT_NO_SEQ"."SEQ" + 1
                RETURNING "SEQ" AS "Value";
                """,
                year)
            .ToListAsync(cancellationToken);

        return $"CC-{year:D4}-{rows.Single():D4}";
    }
}

public sealed record CreateCycleCountRequest(IReadOnlyList<long> BinIds, string ActingPerson);

public sealed record SubmitCountLinesRequest(string BinCode, IReadOnlyList<CountLineInput> Lines, bool Recount, string ActingPerson);

public sealed record ConfirmCycleCountRequest(IReadOnlyList<CountReasonInput> Reasons, string ActingPerson);

public sealed record CancelCycleCountRequest(string ActingPerson);

// Counting lines is Staff work; creating, reviewing (system quantities appear here), confirming and cancelling are Inventory actions
// (capability-shaped, enforced for real in S8, SEC-08). The blind read never carries a system quantity (CC-2).
internal static class CycleCountEndpoints
{
    private const long MaxBodyBytes = 256 * 1024; // up to 2,000 counted lines per bin

    public static void MapCycleCountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var staff = endpoints.MapSecuredGroup("/api/warehouse/cycle-counts", Permissions.InventoryRead).WithTags("Warehouse")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        staff.MapGet("", async (
                short? statusId,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchCycleCountsQuery(statusId, page, pageSize), cancellationToken)));

        staff.MapGet("/bin-age", async (string? zone, ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new BinAgeQuery(zone, page, pageSize), cancellationToken)));

        staff.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetCycleCountQuery(id), cancellationToken)));

        staff.MapPut("/{id:long}/lines", async (long id, SubmitCountLinesRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SubmitCountLinesCommand(id, body.BinCode, body.Lines ?? [], body.Recount, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.WarehouseWrite);

        var inventory = endpoints.MapSecuredGroup("/api/warehouse/cycle-counts", Permissions.InventoryWrite).WithTags("Warehouse")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes));

        inventory.MapPost("", async (CreateCycleCountRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new CreateCycleCountCommand(body.BinIds ?? [], body.ActingPerson), cancellationToken))).RequirePermission(Permissions.InventoryWrite)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        inventory.MapGet("/{id:long}/review", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ReviewCountQuery(id), cancellationToken)));

        inventory.MapPost("/{id:long}/confirm", async (long id, ConfirmCycleCountRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ConfirmCycleCountCommand(id, body.Reasons ?? [], body.ActingPerson), cancellationToken))).RequirePermission(Permissions.InventoryWrite)
            .RequireRateLimiting(RateLimitPolicies.Strict);

        inventory.MapPost("/{id:long}/cancel", async (long id, CancelCycleCountRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new CancelCycleCountCommand(id, body.ActingPerson), cancellationToken))).RequirePermission(Permissions.InventoryWrite);
    }
}
