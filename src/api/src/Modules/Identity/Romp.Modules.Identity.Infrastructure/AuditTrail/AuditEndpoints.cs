using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.AuditTrail;

namespace Romp.Modules.Identity.Infrastructure.AuditTrail;

/// <summary>
/// The audit trail is read-only (audit-trail AC-4, AC-8, AC-12): two GET routes behind <c>audit.read</c> and nothing that writes.
/// </summary>
internal static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var audit = endpoints.MapSecuredGroup("/api/identity/audit", Permissions.AuditRead).WithTags("Identity")
            .AddEndpointFilter<Sessions.CsrfFilter>();

        audit.MapGet("", async (
                ISender sender,
                CancellationToken cancellationToken,
                string? actor = null,
                string? module = null,
                string? entityType = null,
                string? entityKey = null,
                string? operation = null,
                DateTimeOffset? from = null,
                DateTimeOffset? to = null,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListAuditQuery(actor, module, entityType, entityKey, operation, from, to, page, pageSize), cancellationToken)));

        audit.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetAuditEntryQuery(id), cancellationToken)));
    }
}
