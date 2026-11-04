using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Notifications.Application.DeliveryLog;

namespace Romp.Modules.Notifications.Infrastructure.DeliveryLog;

/// <summary>The body of a resend: a key the screen makes once per dialog (a double click sends it twice), and the staff member's yes to a duplicate warning.</summary>
public sealed record ResendRequest(string? Key, bool AcknowledgeDuplicate = false);

/// <summary>
/// The staff delivery log (G3, NFR-FT-07, FR-63). Reading needs <c>notifications.view</c>, resending <c>notifications.resend</c>, both checked
/// server-side on every request (SEC-08, DLG-4). Rows are channel deliveries, so a notification sent by email and WhatsApp shows twice. Recipients
/// are masked and no message body is returned (DLG-6, SEC-25). The acting staff member comes from the signed-in account, never from the body.
/// </summary>
internal static class DeliveryLogEndpoints
{
    private const long MaxBodyBytes = 1024;
    private const int MaxSearchLength = 100;

    public static void MapDeliveryLogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var read = endpoints.MapSecuredGroup("/api/admin/notifications", Permissions.NotificationsView).WithTags("Notifications");

        read.MapGet("", async (
            DeliveryLogQueries queries,
            CancellationToken cancellationToken,
            string? status = null,
            string? channel = null,
            string? kind = null,
            bool deadLetters = false,
            long? orderId = null,
            string? search = null,
            int page = 1,
            int pageSize = PageRequest.DefaultPageSize) =>
        {
            if (search is { Length: > MaxSearchLength })
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["search"] = [$"Search for at most {MaxSearchLength} characters."] });
            }

            var filter = new DeliveryLogFilter(status, channel, kind, deadLetters, orderId, search);
            return Results.Ok(await queries.ListAsync(filter, new PageRequest(page, pageSize), cancellationToken));
        });

        read.MapGet("/summary", async (DeliveryLogQueries queries, CancellationToken cancellationToken) =>
            Results.Ok(await queries.SummaryAsync(cancellationToken)));

        read.MapGet("/{id:long}", async (long id, DeliveryLogQueries queries, CancellationToken cancellationToken) =>
            await queries.GetAsync(id, cancellationToken) is { } found ? Results.Ok(found) : Results.NotFound());

        endpoints.MapSecuredGroup("/api/admin/notifications", Permissions.NotificationsResend).WithTags("Notifications")
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .MapPost("/{id:long}/resend", async (long id, ResendRequest body, ResendNotification resend, ICurrentActor actor, CancellationToken cancellationToken) =>
            {
                var result = await resend.ExecuteAsync(id, body.Key ?? string.Empty, body.AcknowledgeDuplicate, actor.UserName, cancellationToken);
                return result.Outcome switch
                {
                    ResendOutcome.Queued => Results.Accepted($"/api/admin/notifications/{result.NewDeliveryId}", new { id = result.NewDeliveryId }),
                    ResendOutcome.Replayed => Results.Ok(new { id = result.NewDeliveryId }),
                    ResendOutcome.NotFound => Results.NotFound(),
                    ResendOutcome.NeedsConfirmation => Results.Conflict(new { code = "duplicate-warning", title = "This message was probably received. Confirm to send it again." }),
                    ResendOutcome.AlreadyInProgress => Results.Conflict(new { code = "in-progress", title = "A resend of this message is already waiting to be sent." }),
                    ResendOutcome.KeyReused => Results.Conflict(new { code = "key-reused", title = "That resend key belongs to a different message. Reload and try again." }),
                    _ => Results.Conflict(new { code = "not-resendable", title = "This message is still queued or was skipped, so there is nothing to resend." }),
                };
            });
    }
}
