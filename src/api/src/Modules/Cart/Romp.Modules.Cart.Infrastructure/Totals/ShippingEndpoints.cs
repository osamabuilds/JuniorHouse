using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Cart.Application.Totals;

namespace Romp.Modules.Cart.Infrastructure.Totals;

/// <summary>The fee for one city. Only the fee: the city is in the route and nothing else can be sent.</summary>
public sealed record SetShippingRateRequest(decimal Fee);

/// <summary>The shop-wide delivery settings (Q-TOT-1). Empty free-delivery amount and Cash on Delivery fee mean "none".</summary>
public sealed record SetShippingSettingsRequest(decimal DefaultFee, decimal? FreeThreshold, decimal? CodFee);

/// <summary>
/// Staff set what delivery costs per city (cart T3, TOT-5). Every route needs the <c>shipping.manage</c> permission, checked on every request
/// (SEC-08). A change applies from the next basket read; placed orders keep the fee they were placed with.
/// </summary>
internal static class ShippingEndpoints
{
    private const long MaxBodyBytes = 1024;

    public static void MapShippingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var reads = endpoints.MapSecuredGroup("/api/shipping", Permissions.ShippingManage).WithTags("Cart");

        reads.MapGet("/rates", async (ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new ListShippingRatesQuery(page, pageSize), cancellationToken)));

        reads.MapGet("/settings", async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetShippingSettingsQuery(), cancellationToken)));

        endpoints.MapSecuredGroup("/api/shipping", Permissions.ShippingManage).WithTags("Cart")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .MapPut("/rates/{cityId:int}", async (int cityId, SetShippingRateRequest body, ISender sender, CancellationToken cancellationToken) =>
                cityId is > 0 and <= short.MaxValue
                    ? Results.Ok(await sender.Send(new SetShippingRateCommand((short)cityId, body.Fee), cancellationToken))
                    : Results.NotFound());

        endpoints.MapSecuredGroup("/api/shipping", Permissions.ShippingManage).WithTags("Cart")
            .MapDelete("/rates/{cityId:int}", async (int cityId, ISender sender, CancellationToken cancellationToken) =>
            {
                if (cityId is > 0 and <= short.MaxValue)
                {
                    await sender.Send(new RemoveShippingRateCommand((short)cityId), cancellationToken);
                }

                return Results.NoContent();
            });

        endpoints.MapSecuredGroup("/api/shipping", Permissions.ShippingManage).WithTags("Cart")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .MapPut("/settings", async (SetShippingSettingsRequest body, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new SetShippingSettingsCommand(body.DefaultFee, body.FreeThreshold, body.CodFee), cancellationToken)));
    }
}
