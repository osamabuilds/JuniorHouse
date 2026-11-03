using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Domain.Channels;

namespace Romp.Modules.Notifications.Application.Channels;

/// <summary>
/// WhatsApp settings (<c>Notifications:WhatsApp</c>). The secrets come from configuration or user secrets, never from the repository. The
/// webhook fails closed: with no app secret every inbound event is refused.
/// </summary>
public sealed class WhatsAppSettings
{
    public const string SectionName = "Notifications:WhatsApp";

    public bool Enabled { get; set; }

    public string? PhoneNumberId { get; set; }

    public string? AccessToken { get; set; }

    /// <summary>The app secret the provider signs each event with (SEC-10).</summary>
    public string? AppSecret { get; set; }

    /// <summary>The token the provider sends once, when the webhook is registered (GET verification).</summary>
    public string? VerifyToken { get; set; }

    /// <summary>The salt for the STOP list: numbers are stored as salted hashes, never in clear (CHN-6).</summary>
    public string? NumberSalt { get; set; }

    /// <summary>The largest webhook body accepted, in bytes (64 KB).</summary>
    public int MaxBodyBytes { get; set; } = 64 * 1024;
}

/// <summary>Checks the <c>X-Hub-Signature-256</c> header: <c>sha256=</c> and the hex HMAC-SHA256 of the raw body under the app secret.</summary>
public static class WhatsAppSignature
{
    public const string HeaderName = "X-Hub-Signature-256";

    private const string Prefix = "sha256=";

    public static string Compute(string appSecret, ReadOnlySpan<byte> body)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        return Prefix + Convert.ToHexStringLower(mac);
    }

    public static bool IsValid(string? appSecret, ReadOnlySpan<byte> body, string? header)
    {
        if (string.IsNullOrEmpty(appSecret) || string.IsNullOrEmpty(header))
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Compute(appSecret, body));
        var given = Encoding.ASCII.GetBytes(header.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }
}

/// <summary>Turns Meta's webhook JSON into the few facts routing needs: button replies, STOP and delivery receipts. Anything else is ignored.</summary>
public static class WhatsAppPayloadParser
{
    /// <summary>Throws <see cref="JsonException"/> when the body is not JSON at all.</summary>
    public static IReadOnlyList<WhatsAppInboundEvent> Parse(ReadOnlySpan<byte> body)
    {
        using var doc = JsonDocument.Parse(body.ToArray());
        var events = new List<WhatsAppInboundEvent>();
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return events;
        }

        foreach (var value in entries.EnumerateArray().SelectMany(Changes).Select(c => c.TryGetProperty("value", out var v) ? v : default).Where(v => v.ValueKind == JsonValueKind.Object))
        {
            foreach (var message in Items(value, "messages"))
            {
                if (ReadMessage(message) is { } inbound)
                {
                    events.Add(inbound);
                }
            }

            foreach (var status in Items(value, "statuses"))
            {
                var id = Text(status, "id");
                var state = Text(status, "status");
                if (id is not null && state is not null)
                {
                    events.Add(new WhatsAppInboundEvent($"{id}:{state}", WhatsAppEventType.Receipt, Number(Text(status, "recipient_id")), id, state));
                }
            }
        }

        return events;
    }

    private static IEnumerable<JsonElement> Changes(JsonElement entry) => Items(entry, "changes");

    private static IEnumerable<JsonElement> Items(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object)
            : [];

    private static WhatsAppInboundEvent? ReadMessage(JsonElement message)
    {
        var id = Text(message, "id");
        if (id is null)
        {
            return null;
        }

        var from = Number(Text(message, "from"));
        var replyTo = message.TryGetProperty("context", out var context) ? Text(context, "id") : null;
        switch (Text(message, "type"))
        {
            case "interactive" when message.TryGetProperty("interactive", out var interactive)
                && interactive.TryGetProperty("button_reply", out var reply):
                return new WhatsAppInboundEvent(id, WhatsAppEventType.ButtonReply, from, replyTo, Text(reply, "id"));
            case "button" when message.TryGetProperty("button", out var button):
                return new WhatsAppInboundEvent(id, WhatsAppEventType.ButtonReply, from, replyTo, Text(button, "payload"));
            case "text" when message.TryGetProperty("text", out var text)
                && string.Equals(Text(text, "body")?.Trim(), "STOP", StringComparison.OrdinalIgnoreCase):
                return new WhatsAppInboundEvent(id, WhatsAppEventType.Stop, from, null, null);
            default:
                return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Number(string? digits) => string.IsNullOrEmpty(digits) ? string.Empty : "+" + digits.TrimStart('+');
}

/// <summary>Acts on one kind of verified inbound event. Returns the delivery it answered, when it could tell (H6 routes replies, H7 stops, H8 receipts).</summary>
public interface IWhatsAppInboundHandler
{
    WhatsAppEventType EventType { get; }

    Task<long?> HandleAsync(WhatsAppInboundEvent inbound, CancellationToken cancellationToken);
}

public enum WebhookOutcome
{
    Accepted,
    InvalidSignature,
    BadPayload,
    TooLarge,
}

/// <summary>
/// The inbound WhatsApp webhook (CHN-11, SEC-10): the signature is checked on the raw bytes before anything is parsed; a refused event is stored
/// as unverified and logged as a security event; a verified event is stored once by the provider's event id, so a replay does nothing; each is
/// then handed to the handler for its type. An event stored but not yet processed (a handler failed) is tried again when the provider re-sends it.
/// </summary>
public sealed class WhatsAppWebhookProcessor(
    INotificationsDbContext dbContext,
    WhatsAppSettings settings,
    IEnumerable<IWhatsAppInboundHandler> handlers,
    TimeProvider clock,
    ILogger<WhatsAppWebhookProcessor> logger)
{
    public async Task<WebhookOutcome> HandleAsync(byte[] body, string? signatureHeader, string clientIp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body.Length > settings.MaxBodyBytes)
        {
            return WebhookOutcome.TooLarge;
        }

        var bodyHash = Convert.ToHexStringLower(SHA256.HashData(body));
        if (!WhatsAppSignature.IsValid(settings.AppSecret, body, signatureHeader))
        {
            logger.WebhookSignatureRejected("whatsapp", string.IsNullOrEmpty(signatureHeader) ? "missing" : "mismatch", clientIp);
            await StoreRejectedAsync(bodyHash, cancellationToken);
            return WebhookOutcome.InvalidSignature;
        }

        IReadOnlyList<WhatsAppInboundEvent> events;
        try
        {
            events = WhatsAppPayloadParser.Parse(body);
        }
        catch (JsonException)
        {
            return WebhookOutcome.BadPayload;
        }

        foreach (var inbound in events)
        {
            await ProcessAsync(inbound, bodyHash, cancellationToken);
        }

        return WebhookOutcome.Accepted;
    }

    private async Task StoreRejectedAsync(string bodyHash, CancellationToken cancellationToken)
    {
        // One row per distinct refused body: repeating the same forged request only repeats the log line.
        var eventId = "rejected:" + bodyHash;
        if (await dbContext.WhatsAppEvents.AnyAsync(e => e.ProviderEventId == eventId, cancellationToken))
        {
            return;
        }

        dbContext.WhatsAppEvents.Add(new WhatsAppEvent(eventId, null, false, bodyHash, clock.GetUtcNow()));
        await TrySaveAsync(cancellationToken);
    }

    private async Task ProcessAsync(WhatsAppInboundEvent inbound, string bodyHash, CancellationToken cancellationToken)
    {
        var record = await dbContext.WhatsAppEvents.FirstOrDefaultAsync(e => e.ProviderEventId == inbound.EventId, cancellationToken);
        if (record is { ProcessedAt: not null })
        {
            return; // a replay: nothing happens twice
        }

        if (record is null)
        {
            record = new WhatsAppEvent(inbound.EventId, inbound.Type, true, bodyHash, clock.GetUtcNow());
            dbContext.WhatsAppEvents.Add(record);
            if (!await TrySaveAsync(cancellationToken))
            {
                return; // another request stored the same event first and will process it
            }
        }

        long? matched = null;
        foreach (var handler in handlers.Where(h => h.EventType == inbound.Type))
        {
            matched = await handler.HandleAsync(inbound, cancellationToken) ?? matched;
        }

        record.MarkProcessed(clock.GetUtcNow(), matched);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            foreach (var entry in dbContext.WhatsAppEvents.Local.Where(e => e.Id == 0).ToList())
            {
                dbContext.WhatsAppEvents.Remove(entry);
            }

            return false;
        }
    }
}
