using System.Net;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Domain.Payments;
using Romp.Modules.Orders.Infrastructure.Checkout;

namespace Romp.Modules.Orders.Infrastructure.Payments;

/// <summary>The signed token from placing the order, sent to start or follow a payment (PAY-3). It names one order and nothing else.</summary>
public sealed record PaymentTokenRequest(string? Token);

public sealed record StartPaymentResponse(string Kind, string? RedirectUrl);

/// <summary>
/// Online payment endpoints (orders payments Y3, Y4, Y9). The webhook is anonymous by nature: the gateway holds no session, so its only proof
/// is the signature over the body, checked before anything is read (SEC-10). It is rate limited, its body is capped at 64 KB, a rejected
/// message answers 401 and is kept as a security event, and a repeated delivery answers 200 and changes nothing (PAY-5). Starting and
/// following a payment need the signed token for that one order (SEC-05): no order id is ever taken from the request.
/// </summary>
internal static partial class PaymentEndpoints
{
    public const int MaxWebhookBytes = 64 * 1024;
    private const long MaxStartBodyBytes = 2 * 1024;

    [GeneratedRegex("^fk_[0-9a-f]{24}$")]
    private static partial Regex FakeRefPattern();

    public static void MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var webhook = endpoints.MapSecuredGroup("/api/orders/payments", Policies.Public)
            .WithTags("Payments")
            .AddEndpointFilter<OrdersNoStoreFilter>()
            .RequireRateLimiting(RateLimitPolicies.Webhook);

        webhook.MapPost("/webhook/{provider}", async (string provider, HttpContext http, ISender sender, CancellationToken cancellationToken) =>
        {
            var body = await ReadCappedAsync(http.Request, MaxWebhookBytes, cancellationToken);
            if (body is null)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in http.Request.Headers)
            {
                headers[header.Key] = header.Value.ToString();
            }

            try
            {
                var result = await sender.Send(new HandlePaymentWebhookCommand(provider, body, headers), cancellationToken);
                return result.Kind switch
                {
                    WebhookResultKind.Accepted or WebhookResultKind.Duplicate => Results.Ok(),
                    WebhookResultKind.Rejected => Results.StatusCode(StatusCodes.Status401Unauthorized),
                    _ => Results.NotFound(),
                };
            }
            catch (DbUpdateException)
            {
                // Two deliveries of one event raced and the database let the first in: this one is the repeat (PAY-5).
                return Results.Ok();
            }
        });

        var pay = endpoints.MapSecuredGroup("/api/orders/payments", Policies.Public)
            .WithTags("Payments")
            .WithMetadata(new RequestSizeLimitAttribute(MaxStartBodyBytes))
            .AddEndpointFilter<OrdersNoStoreFilter>();

        pay.MapPost("/start", async (PaymentTokenRequest body, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new StartOnlinePaymentCommand(body.Token), cancellationToken);
            var response = new StartPaymentResponse(result.Kind.ToString(), result.RedirectUrl);
            return result.Kind switch
            {
                StartPaymentKind.Started or StartPaymentKind.Reused => Results.Ok(response),
                StartPaymentKind.InvalidLink or StartPaymentKind.NotOnline => Results.NotFound(),
                StartPaymentKind.GatewayUnavailable => Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
                _ => Results.Json(response, statusCode: StatusCodes.Status409Conflict),
            };
        })
        .AddEndpointFilter<OrdersOriginCheckFilter>()
        .RequireRateLimiting(RateLimitPolicies.Strict);

        pay.MapGet("/status", async (string? token, ISender sender, CancellationToken cancellationToken) =>
            await sender.Send(new GetPaymentStatusQuery(token), cancellationToken) is { } status ? Results.Ok(status) : Results.NotFound())
            .RequireRateLimiting(RateLimitPolicies.Strict);

        MapFakeGatewayPages(endpoints);
    }

    /// <summary>
    /// The pretend hosted payment page, for the local stack only. Mapped only when the fake gateway is switched on, which the host refuses
    /// outside Development and Testing (PAY-15). The buttons make the fake gateway answer as a real one would, including a charge whose
    /// callback is lost (to see reconciliation work).
    /// </summary>
    private static void MapFakeGatewayPages(IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<OrdersOptions>>().Value;
        if (options.GatewayProvider != "fake")
        {
            return;
        }

        var fake = endpoints.MapSecuredGroup("/api/orders/payments/fake", Policies.Public).WithTags("Payments").ExcludeFromDescription();

        fake.MapGet("/{gatewayRef}", (string gatewayRef, FakeGateway gateway) =>
        {
            if (!FakeRefPattern().IsMatch(gatewayRef) || gateway.Find(gatewayRef) is not { } session)
            {
                return Results.NotFound();
            }

            var amount = WebUtility.HtmlEncode(session.Amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture));
            var order = WebUtility.HtmlEncode(session.OrderNumber);
            var html = $$"""
                <!doctype html>
                <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
                <meta name="robots" content="noindex, nofollow"><title>Test payment page</title>
                <style>body{font-family:system-ui,sans-serif;max-width:28rem;margin:3rem auto;padding:0 1rem}button{display:block;width:100%;margin:.5rem 0;padding:.8rem;font-size:1rem}</style></head>
                <body><main><h1>Test payment page</h1>
                <p>This is a stand-in for the real gateway (local use only). Order <strong>{{order}}</strong>, Rs {{amount}}.</p>
                <form method="post" action="{{gatewayRef}}/complete">
                <button type="submit" name="outcome" value="paid">Pay now</button>
                <button type="submit" name="outcome" value="failed">Decline the card</button>
                <button type="submit" name="outcome" value="paid-lost">Pay, but lose the callback</button>
                </form></main></body></html>
                """;
            return Results.Content(html, "text/html");
        });

        fake.MapPost("/{gatewayRef}/complete", async ([FromForm] string outcome, string gatewayRef, FakeGateway gateway, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!FakeRefPattern().IsMatch(gatewayRef) || gateway.Find(gatewayRef) is not { } session)
            {
                return Results.NotFound();
            }

            var result = outcome == "failed" ? GatewayOutcome.Failed : GatewayOutcome.Paid;
            gateway.SetOutcome(gatewayRef, result);
            if (outcome != "paid-lost")
            {
                var (body, headers) = gateway.BuildCallback(gatewayRef, result);
                await sender.Send(new HandlePaymentWebhookCommand(gateway.Provider, body, headers), cancellationToken);
            }

            return Results.Redirect(session.ReturnUrl, permanent: false);
        })
        .DisableAntiforgery();
    }

    /// <summary>Reads the body but never more than <paramref name="max"/> bytes; null when it is longer.</summary>
    private static async Task<byte[]?> ReadCappedAsync(HttpRequest request, int max, CancellationToken cancellationToken)
    {
        if (request.ContentLength > max)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > max)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
