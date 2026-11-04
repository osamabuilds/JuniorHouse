using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Notifications.Application.Channels;

namespace Romp.Modules.Notifications.Infrastructure.Channels;

/// <summary>
/// The WhatsApp provider's callback (CHN-11). Anonymous by nature: the provider holds no session, so the proof is the signature on the body,
/// checked before anything is read. GET answers the one-time verification handshake; POST receives events. Rate limited, body capped.
/// </summary>
internal static class WhatsAppWebhookEndpoints
{
    public static void MapWhatsAppWebhook(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/webhooks/whatsapp", Policies.Public)
            .WithTags("Notifications")
            .RequireRateLimiting(RateLimitPolicies.Webhook);

        group.MapGet(string.Empty, (
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? token,
            [FromQuery(Name = "hub.challenge")] string? challenge,
            WhatsAppSettings settings) =>
            mode == "subscribe" && challenge is not null && TokenMatches(settings.VerifyToken, token)
                ? Results.Text(challenge, "text/plain")
                : Results.StatusCode(StatusCodes.Status403Forbidden));

        group.MapPost(string.Empty, async (HttpContext http, WhatsAppWebhookProcessor processor, WhatsAppSettings settings, CancellationToken cancellationToken) =>
        {
            var body = await ReadCappedAsync(http.Request, settings.MaxBodyBytes, cancellationToken);
            if (body is null)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var outcome = await processor.HandleAsync(
                body, http.Request.Headers[WhatsAppSignature.HeaderName].ToString(), http.Connection.RemoteIpAddress?.ToString() ?? "unknown", cancellationToken);
            return outcome switch
            {
                WebhookOutcome.Accepted => Results.Ok(),
                WebhookOutcome.InvalidSignature => Results.StatusCode(StatusCodes.Status401Unauthorized),
                WebhookOutcome.TooLarge => Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
                _ => Results.StatusCode(StatusCodes.Status400BadRequest),
            };
        });
    }

    private static bool TokenMatches(string? expected, string? given) =>
        !string.IsNullOrEmpty(expected) && !string.IsNullOrEmpty(given)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));

    /// <summary>Reads the body but never more than <paramref name="max"/> bytes; returns null when it is longer.</summary>
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
