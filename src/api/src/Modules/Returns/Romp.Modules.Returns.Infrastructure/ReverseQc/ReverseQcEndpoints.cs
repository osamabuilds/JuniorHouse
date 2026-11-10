using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Returns.Application.ReverseQc;

namespace Romp.Modules.Returns.Infrastructure.ReverseQc;

public sealed record ReceiveCaseRequest(bool SealIntact, string? BinCode);

public sealed record DecideLineRequest(string? Decision, int Quantity, string? Note);

/// <summary>
/// Reverse QC (reverse-qc spec, RQC-12): reading needs <c>shipping.view</c>, receiving, deciding and completing need <c>shipping.ops</c>. The staff member
/// comes from the session. The body is size limited.
/// </summary>
internal static class ReverseQcEndpoints
{
    public static void MapReverseQcEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/returns/cases", Permissions.ShippingView).WithTags("Returns")
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        group.MapGet(string.Empty, async (ISender sender, CancellationToken cancellationToken, string? status = null, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListReturnCasesQuery(status, page, pageSize), cancellationToken)));

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetReturnCaseQuery(id), cancellationToken) is { } detail ? Results.Ok(detail) : Results.NotFound());

        group.MapPost("/{id:long}/receive", async (long id, ReceiveCaseRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new ReceiveReturnCaseCommand(id, body.SealIntact, body.BinCode, actor.UserName), cancellationToken) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequirePermission(Permissions.ShippingOps);

        group.MapPost("/{id:long}/lines/{lineId:long}/decide", async (long id, long lineId, DecideLineRequest body, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new DecideReturnLineCommand(id, lineId, body.Decision, body.Quantity, body.Note, actor.UserName), cancellationToken) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequirePermission(Permissions.ShippingOps);

        group.MapPost("/{id:long}/complete", async (long id, ISender sender, ICurrentActor actor, CancellationToken cancellationToken) =>
            await sender.Send(new CompleteReturnCaseCommand(id, actor.UserName), cancellationToken) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequirePermission(Permissions.ShippingOps);
    }
}
