using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Orders;

namespace Romp.Modules.Orders.Infrastructure.Checkout;

/// <summary>
/// What the browser sends to place an order: who, where, how to pay. No price, total, discount or quantity exists in it (SEC-09, ORD-4). The
/// <c>Idempotency-Key</c> travels in a header, made once per checkout page load (CHK-10).
/// </summary>
public sealed record PlaceOrderRequest(CheckoutContact? Contact, CheckoutAddress? Address, long? SavedAddressId, string? PaymentMethod);

/// <summary>What the browser gets for a refused order: why, in words, and which variants were short so the bag can point at them.</summary>
public sealed record OrderRefusedResponse(string Reason, IReadOnlyList<string> Messages, IReadOnlyList<long> ShortVariantIds);

public sealed record OrderPlacedResponse(string OrderNumber, bool WasRepeat);

/// <summary>Writes with a cookie and no anti-forgery token need the browser <c>Origin</c> to be the site's (the rule in <see cref="OriginCheckFilterBase"/>).</summary>
internal sealed class OrdersOriginCheckFilter(IOptions<SiteOptions> site) : OriginCheckFilterBase(site);

internal sealed class OrdersNoStoreFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "private, no-store";
        return await next(context);
    }
}

/// <summary>
/// Checkout (orders plan, "API endpoints"). Open to guests by design: the basket belongs to the session or the guest cookie and is never
/// named in the request, so another shopper basket cannot be reached (SEC-05). Placing an order also checks the browser <c>Origin</c>, is
/// rate limited, and caps the body at 8 KB.
/// </summary>
internal static class CheckoutEndpoints
{
    private const long MaxBodyBytes = 8 * 1024;

    public static void MapCheckoutEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var orders = endpoints.MapSecuredGroup("/api/orders", Policies.Public)
            .WithTags("Orders")
            .AddEndpointFilter<OrdersNoStoreFilter>();

        orders.MapGet("/checkout", async (ISender sender, CancellationToken cancellationToken, int? cityId = null, string? method = null) =>
            Results.Ok(await sender.Send(new GetCheckoutSummaryQuery(cityId, method), cancellationToken)));

        orders.MapGet("/payment-methods", (IOptions<OrdersOptions> options) => Results.Ok(PaymentMethodCatalog.Available(options.Value)));

        endpoints.MapSecuredGroup("/api/orders", Policies.Public)
            .WithTags("Orders")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .AddEndpointFilter<OrdersNoStoreFilter>()
            .MapPost("", async (
                PlaceOrderRequest body,
                [FromHeader(Name = "Idempotency-Key")] string? key,
                ISender sender,
                IOptions<OrdersOptions> options,
                CancellationToken cancellationToken) =>
            {
                // Only a method the shop offers now can be chosen: an online method stays off until a payment gateway is switched on (PAY-7).
                if (PaymentMethodCatalog.IdOf(body.PaymentMethod) is not { } methodId
                    || PaymentMethodCatalog.Available(options.Value).All(m => m.Id != methodId))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["paymentMethod"] = ["Choose a payment method."] });
                }

                var result = await sender.Send(
                    new PlaceOrderCommand(key ?? string.Empty, body.Contact ?? new CheckoutContact(null, null, null, false), body.Address, body.SavedAddressId, methodId),
                    cancellationToken);
                return result.Outcome == PlaceOrderOutcome.Placed
                    ? Results.Json(new OrderPlacedResponse(result.OrderNumber!, result.WasRepeat), statusCode: result.WasRepeat ? StatusCodes.Status200OK : StatusCodes.Status201Created)
                    : Results.Json(new OrderRefusedResponse(result.Refusal.ToString(), result.Messages, result.ShortVariantIds), statusCode: StatusCodes.Status409Conflict);
            })
            .AddEndpointFilter<OrdersOriginCheckFilter>()
            .RequireRateLimiting(RateLimitPolicies.Strict);
    }
}
