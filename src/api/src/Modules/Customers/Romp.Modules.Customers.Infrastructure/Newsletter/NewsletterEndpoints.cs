using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Application.Newsletter;

namespace Romp.Modules.Customers.Infrastructure.Newsletter;

public sealed record SubscribeNewsletterRequest(string? Email);

public sealed record SetMyNewsletterRequest(bool Subscribed);

/// <summary>
/// Newsletter consent (FR-41). The public routes take an email or a signed link and nothing else; their answer is the same whether the
/// address is new, already subscribed or unknown, and they share the strict rate limit (NLS-6). The account routes act for the signed-in
/// customer on their own email only (NLS-2). No route takes a customer id (SEC-05). Every answer is <c>no-store</c>.
/// </summary>
internal static class NewsletterEndpoints
{
    private const long MaxBodyBytes = 2 * 1024;

    public static void MapNewsletterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var open = endpoints.MapSecuredGroup("/api/newsletter", Policies.Public)
            .RequireRateLimiting(RateLimitPolicies.Strict)
            .WithTags("Customers")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxBodyBytes))
            .AddEndpointFilter<CustomerNoStoreFilter>();

        open.MapPost("/subscribe", Subscribe).AddEndpointFilter<CustomerOriginCheckFilter>();

        // The link in an email. GET performs it (idempotent, so a mail scanner that opens it only errs towards not mailing); POST is the
        // one-click form mail clients send (List-Unsubscribe-Post). Neither takes an Origin check: a mail client has none. The signed token
        // is the only authority, and it names one record.
        open.MapGet("/unsubscribe", Unsubscribe);
        open.MapPost("/unsubscribe", Unsubscribe);

        var mine = endpoints.MapSecuredGroup("/api/customers/me/newsletter", Policies.Customer)
            .WithTags("Customers")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxBodyBytes))
            .AddEndpointFilter<CustomerNoStoreFilter>();

        mine.MapGet(string.Empty, async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetMyNewsletterQuery(), cancellationToken)));
        mine.MapPut(string.Empty, async (SetMyNewsletterRequest body, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new SetMyNewsletterCommand(body.Subscribed), cancellationToken)));
    }

    private static async Task<IResult> Subscribe(SubscribeNewsletterRequest body, ISender sender, CancellationToken cancellationToken)
    {
        await sender.Send(new SubscribeNewsletterCommand(body.Email), cancellationToken);
        return Results.Ok(new { subscribed = true });
    }

    private static async Task<IResult> Unsubscribe(string? token, ISender sender, CancellationToken cancellationToken)
    {
        // A link that was not signed by us says so; a signed link for a record that is gone is simply done. Neither shows whether an
        // address exists (NLS-3).
        var valid = await sender.Send(new UnsubscribeByTokenCommand(token), cancellationToken);
        return valid
            ? Results.Ok(new { unsubscribed = true })
            : Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "This unsubscribe link is not valid.");
    }
}
