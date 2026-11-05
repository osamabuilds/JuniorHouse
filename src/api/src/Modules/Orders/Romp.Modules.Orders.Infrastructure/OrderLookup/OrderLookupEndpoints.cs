using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Orders.Application.OrderLookup;
using Romp.Modules.Orders.Infrastructure.Checkout;

namespace Romp.Modules.Orders.Infrastructure.OrderLookup;

public sealed record LookupOrderRequest(string? OrderNumber, string? Mobile, string? CaptchaToken);

/// <summary>
/// Guest order look-up (FR-37, SEC-06, SEC-07). Public, so it is rate limited, takes only a small body, checks the browser Origin and is
/// never cached. Every mismatch is the same 404 with the same body; the only other answers depend on the address of the caller, not on the order.
/// </summary>
internal static class OrderLookupEndpoints
{
    internal const string MismatchTitle = "Those details do not match an order.";

    public static void MapOrderLookupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/orders/lookup", Policies.Public)
            .WithTags("Orders")
            .WithMetadata(new RequestSizeLimitAttribute(2 * 1024))
            .AddEndpointFilter<OrdersNoStoreFilter>();

        group.MapPost(string.Empty, Lookup)
            .AddEndpointFilter<OrdersOriginCheckFilter>()
            .RequireRateLimiting(RateLimitPolicies.Strict);

        group.MapGet("/link", ViewByLink)
            .RequireRateLimiting(RateLimitPolicies.Strict);
    }

    private static async Task<IResult> Lookup(LookupOrderRequest body, ISender sender, HttpContext http, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LookupOrderQuery(body.OrderNumber, body.Mobile, body.CaptchaToken, http.ClientIp()), cancellationToken);
        return result.Status switch
        {
            LookupStatus.Found => Results.Ok(result.Order),
            LookupStatus.CaptchaRequired => Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired, title: MismatchTitle,
                extensions: new Dictionary<string, object?> { ["captchaRequired"] = true }),
            LookupStatus.RateLimited => Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests, title: "Too many attempts. Please try again later.",
                extensions: new Dictionary<string, object?> { ["captchaRequired"] = true }),
            _ => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: MismatchTitle),
        };
    }

    private static async Task<IResult> ViewByLink(string? token, ISender sender, CancellationToken cancellationToken)
    {
        var order = await sender.Send(new ViewOrderByLinkQuery(token), cancellationToken);
        return order is null ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: MismatchTitle) : Results.Ok(order);
    }
}
