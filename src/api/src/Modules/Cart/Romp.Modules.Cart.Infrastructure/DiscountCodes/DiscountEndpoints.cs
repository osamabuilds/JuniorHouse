using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Cart.Application.DiscountCodes;

namespace Romp.Modules.Cart.Infrastructure.DiscountCodes;

/// <summary>Turn a code on or off.</summary>
public sealed record SetDiscountActiveRequest(bool Active);

/// <summary>
/// Staff manage discount codes (cart D7, FR-57). Every route needs <c>discounts.manage</c>, checked on every request (SEC-08). Changes are
/// audited automatically through the module's outbox (DSC-11). A code is switched off, never deleted, so past orders keep their history.
/// </summary>
internal static class DiscountEndpoints
{
    private const long MaxBodyBytes = 4 * 1024;

    public static void MapDiscountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSecuredGroup("/api/discounts", Permissions.DiscountsManage).WithTags("Cart")
            .MapGet("", async (ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = PageRequest.DefaultPageSize, bool? active = null) =>
                Results.Ok(await sender.Send(new ListDiscountCodesQuery(page, pageSize, active), cancellationToken)));

        endpoints.MapSecuredGroup("/api/discounts", Permissions.DiscountsManage).WithTags("Cart")
            .MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
                await sender.Send(new GetDiscountCodeQuery(id), cancellationToken) is { } found ? Results.Ok(found) : Results.NotFound());

        endpoints.MapSecuredGroup("/api/discounts", Permissions.DiscountsManage).WithTags("Cart")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .MapPost("", async (DiscountCodeInput body, ISender sender, CancellationToken cancellationToken) =>
            {
                var created = await sender.Send(new CreateDiscountCodeCommand(body), cancellationToken);
                return Results.Created($"/api/discounts/{created.Id}", created);
            });

        endpoints.MapSecuredGroup("/api/discounts", Permissions.DiscountsManage).WithTags("Cart")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .MapPut("/{id:long}", async (long id, DiscountCodeInput body, ISender sender, CancellationToken cancellationToken) =>
                await sender.Send(new UpdateDiscountCodeCommand(id, body), cancellationToken) is { } updated ? Results.Ok(updated) : Results.NotFound());

        endpoints.MapSecuredGroup("/api/discounts", Permissions.DiscountsManage).WithTags("Cart")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .MapPut("/{id:long}/active", async (long id, SetDiscountActiveRequest body, ISender sender, CancellationToken cancellationToken) =>
                await sender.Send(new SetDiscountCodeActiveCommand(id, body.Active), cancellationToken) is { } changed ? Results.Ok(changed) : Results.NotFound());
    }
}
