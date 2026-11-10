using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Shipping.Application.CodReconciliation;

namespace Romp.Modules.Shipping.Infrastructure.CodReconciliation;

public sealed record RecordRemittanceRequest(string? Courier, string? Reference, DateOnly RemittedOn, decimal StatedTotal, List<StatementLineInput>? Lines);

public sealed record ResolveLineRequest(string? Note);

/// <summary>
/// COD reconciliation (cod-reconciliation spec, COD-15): reading needs <c>shipping.view</c>, recording a statement and resolving a line need
/// <c>shipping.ops</c>. The staff member comes from the session. The body is size limited (COD-16).
/// </summary>
internal static class CodEndpoints
{
    public static void MapCodEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/shipping/cod", Permissions.ShippingView).WithTags("Shipping")
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024));

        group.MapGet("/summary", async (ISender sender, CancellationToken cancellationToken) => Results.Ok(await sender.Send(new GetCodSummaryQuery(), cancellationToken)));

        group.MapGet("/awaiting", async (ISender sender, CancellationToken cancellationToken, bool overdueOnly = false, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListCodAwaitingQuery(overdueOnly, page, pageSize), cancellationToken)));

        group.MapGet("/exceptions", async (ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListCodExceptionsQuery(page, pageSize), cancellationToken)));

        group.MapGet("/remittances", async (ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListCodRemittancesQuery(page, pageSize), cancellationToken)));

        group.MapGet("/remittances/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetCodRemittanceQuery(id), cancellationToken) is { } detail ? Results.Ok(detail) : Results.NotFound());

        group.MapPost("/remittances", async (RecordRemittanceRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new RecordCodRemittanceCommand(body.Courier ?? string.Empty, body.Reference, body.RemittedOn, body.StatedTotal, body.Lines ?? [], actor.UserName), cancellationToken);
            return result.AlreadyRecorded ? Results.Ok(result) : Results.Created($"/api/shipping/cod/remittances/{result.Remittance.Summary.Id}", result);
        }).RequirePermission(Permissions.ShippingOps);

        group.MapPost("/lines/{id:long}/resolve", async (long id, ResolveLineRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new ResolveCodLineCommand(id, body.Note, actor.UserName), cancellationToken) is { } line ? Results.Ok(line) : Results.NotFound())
            .RequirePermission(Permissions.ShippingOps);
    }
}
