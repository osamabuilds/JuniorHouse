using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Application.DeliveryLog;

/// <summary>How a resend ended. Everything except <see cref="Queued"/> and <see cref="Replayed"/> changed nothing.</summary>
public enum ResendOutcome
{
    /// <summary>A new delivery was queued.</summary>
    Queued,

    /// <summary>The same client key was seen before: the earlier resend is returned, nothing new is queued (DLG-8).</summary>
    Replayed,

    NotFound,

    /// <summary>The delivery is still queued or was deliberately skipped: there is nothing to resend.</summary>
    NotResendable,

    /// <summary>A resend of this delivery is already waiting to be sent (a double click with a fresh key).</summary>
    AlreadyInProgress,

    /// <summary>The message was probably received, so a second copy needs the staff member's confirmation (DLG-7).</summary>
    NeedsConfirmation,

    /// <summary>The key was already used for a different delivery.</summary>
    KeyReused,
}

public sealed record ResendResult(ResendOutcome Outcome, long? NewDeliveryId);

/// <summary>
/// Sends one delivery again from the admin panel (G2, FR-63, DLG-2, 3, 7, 8). Creates a new queued delivery linked to the old one, so the
/// history stays and the normal worker sends it (retries, circuit and suppression all apply). The acting staff member is stamped on the new
/// row and, for a dead letter, on the dead letter; the audit trail records the change on top (FR-59).
/// </summary>
public sealed partial class ResendNotification(INotificationsDbContext dbContext, TimeProvider clock)
{
    [GeneratedRegex("^[A-Za-z0-9_-]{8,64}$")]
    private static partial Regex KeyPattern();

    public async Task<ResendResult> ExecuteAsync(long deliveryId, string key, bool acknowledgeDuplicate, string actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(key) || !KeyPattern().IsMatch(key))
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["key"] = ["Send a resend key of 8 to 64 letters, digits, dashes or underscores."] });
        }

        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new ArgumentException("A resend records who asked for it.", nameof(actor));
        }

        // The same key again is the same click: answer with what the first one did.
        var earlier = await FindByKeyAsync(key, cancellationToken);
        if (earlier is not null)
        {
            return earlier.FallbackOfId == deliveryId
                ? new ResendResult(ResendOutcome.Replayed, earlier.Id)
                : new ResendResult(ResendOutcome.KeyReused, null);
        }

        var notification = await dbContext.Notifications
            .Include(n => n.Deliveries)
            .FirstOrDefaultAsync(n => n.Deliveries.Any(d => d.Id == deliveryId), cancellationToken);
        var original = notification?.Deliveries.FirstOrDefault(d => d.Id == deliveryId);
        if (notification is null || original is null)
        {
            return new ResendResult(ResendOutcome.NotFound, null);
        }

        if (!DeliveryLogCodes.CanResend(original.Status))
        {
            return new ResendResult(ResendOutcome.NotResendable, null);
        }

        if (notification.Deliveries.Any(d => d.FallbackOfId == original.Id && d.Status == DeliveryStatus.Queued))
        {
            return new ResendResult(ResendOutcome.AlreadyInProgress, null);
        }

        if (DeliveryLogCodes.WarnsOfDuplicate(original.Status) && !acknowledgeDuplicate)
        {
            return new ResendResult(ResendOutcome.NeedsConfirmation, null);
        }

        var now = clock.GetUtcNow();
        var resend = notification.AddResend(original, key, now, NotificationPolicy.ValidFor(notification.Kind));

        var dead = await dbContext.DeadLetters.FirstOrDefaultAsync(d => d.DeliveryId == original.Id, cancellationToken);
        dead?.MarkResent(actor, now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two requests with one key raced: the database let only one in. Answer with the winner.
            var winner = await FindByKeyAsync(key, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return new ResendResult(ResendOutcome.Replayed, winner.Id);
        }

        return new ResendResult(ResendOutcome.Queued, resend.Id);
    }

    private Task<ChannelDelivery?> FindByKeyAsync(string key, CancellationToken cancellationToken) =>
        dbContext.Deliveries.AsNoTracking().FirstOrDefaultAsync(d => d.ResendKey == key, cancellationToken);
}
