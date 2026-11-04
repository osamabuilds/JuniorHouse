using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Application.NotifyMe;

namespace Romp.Modules.Customers.Infrastructure.NotifyMe;

public sealed record CreateNotifyRequestBody(long VariantId, string? Email);

/// <summary>
/// Back-in-stock requests (FR-11). The public route takes a variant id and an email and nothing else; it answers the same for a new and a
/// repeated request (NTM-3, NTM-7), shares the strict rate limit, checks the Origin like the other public customer forms and is never
/// cached. The staff route lists counts per variant and nothing personal (NTM-12), behind the inventory permission and a page size of 1..100.
/// </summary>
internal static class NotifyMeEndpoints
{
    private const long MaxBodyBytes = 2 * 1024;

    public static void MapNotifyMeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var open = endpoints.MapSecuredGroup("/api/notify-me", Policies.Public)
            .RequireRateLimiting(RateLimitPolicies.Strict)
            .WithTags("Customers")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxBodyBytes))
            .AddEndpointFilter<CustomerNoStoreFilter>();

        open.MapPost(string.Empty, async (CreateNotifyRequestBody body, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new CreateNotifyRequestCommand(body.VariantId, body.Email), cancellationToken);
                return Results.Ok(new { requested = true });
            })
            .AddEndpointFilter<CustomerOriginCheckFilter>();

        var staff = endpoints.MapSecuredGroup("/api/admin/customers", Permissions.InventoryRead).WithTags("Customers");

        staff.MapGet("/notify-demand", async (
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListNotifyDemandQuery(page, pageSize), cancellationToken)));
    }
}
