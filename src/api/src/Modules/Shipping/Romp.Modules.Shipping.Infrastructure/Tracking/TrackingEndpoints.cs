using System.Text;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Shipping.Application.Tracking;
using Romp.Modules.Shipping.Infrastructure.CourierPort;

namespace Romp.Modules.Shipping.Infrastructure.Tracking;

/// <summary>
/// The two public routes of shipping. The courier webhook has no session: the proof is the signature over the raw body, checked before the body is
/// read as anything, and every failure is the same answer (TRK-2, TRK-11, SEC-07). The tracking look-up shows statuses only, is rate limited, asks
/// for a CAPTCHA after a few failures and answers every miss the same way (TRK-9, TRK-10, SEC-06).
/// </summary>
internal static class TrackingEndpoints
{
    private const int MaxPushBytes = 16 * 1024;
    internal const string NotFoundTitle = "That tracking number does not match a parcel.";

    public static void MapTrackingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var webhooks = endpoints.MapSecuredGroup("/api/shipping/webhooks", Policies.Public).WithTags("Shipping")
            .WithMetadata(new RequestSizeLimitAttribute(MaxPushBytes));
        webhooks.MapPost("/{courier}", ReceivePush).RequireRateLimiting(RateLimitPolicies.Webhook);

        endpoints.MapSecuredGroup("/api/shipping", Policies.Public).WithTags("Shipping")
            .MapGet("/track", Track).RequireRateLimiting(RateLimitPolicies.Strict);
    }

    private static async Task<IResult> ReceivePush(string courier, HttpRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var body = await ReadLimitedAsync(request.Body, MaxPushBytes, cancellationToken);
        if (body is null)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var outcome = await sender.Send(new ApplyCourierPushCommand(courier, body, request.Headers[FakeCourierGateway.SignatureHeader].ToString()), cancellationToken);
        return outcome == PushOutcome.Accepted ? Results.Ok(new { ok = true }) : Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Not accepted.");
    }

    private static async Task<IResult> Track(string? waybill, string? captchaToken, ISender sender, HttpContext http, CancellationToken cancellationToken)
    {
        http.Response.Headers.CacheControl = "no-store";
        var result = await sender.Send(new TrackByWaybillQuery(waybill, captchaToken, http.ClientIp()), cancellationToken);
        return result.Status switch
        {
            TrackLookupStatus.Found => Results.Ok(result.Tracking),
            TrackLookupStatus.CaptchaRequired => Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired, title: NotFoundTitle, extensions: new Dictionary<string, object?> { ["captchaRequired"] = true }),
            _ => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: NotFoundTitle),
        };
    }

    /// <summary>Reads at most <paramref name="max"/> bytes as text; null when the body is longer (the signature is checked only on a body that fits).</summary>
    private static async Task<string?> ReadLimitedAsync(Stream stream, int max, CancellationToken cancellationToken)
    {
        var buffer = new byte[max + 1];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken)) > 0)
        {
            total += read;
        }

        return total > max ? null : Encoding.UTF8.GetString(buffer, 0, total);
    }
}
