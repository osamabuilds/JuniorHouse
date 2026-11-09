using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Domain.Payments;

namespace Romp.Modules.Orders.Infrastructure.Payments;

/// <summary>How the fake gateway behaves. <see cref="Down"/> makes every call fail the way an outage would (SEC-21, PAY-7).</summary>
public enum FakeGatewayMode
{
    Normal,
    Down,
}

/// <summary>One session the fake gateway opened.</summary>
public sealed class FakeSession(string gatewayRef, string attemptReference, string orderNumber, decimal amount, string methodCode, string returnUrl)
{
    public string GatewayRef { get; } = gatewayRef;

    public string AttemptReference { get; } = attemptReference;

    public string OrderNumber { get; } = orderNumber;

    public decimal Amount { get; } = amount;

    public string MethodCode { get; } = methodCode;

    public string ReturnUrl { get; } = returnUrl;

    public GatewayStatusKind Status { get; set; } = GatewayStatusKind.Pending;
}

/// <summary>
/// The stand-in payment gateway that drives every test and the local stack until a real provider is switched on (ADR 0027). It behaves like the
/// real thing where it matters: sessions are idempotent on the attempt reference, callbacks are signed with HMAC-SHA256 over a timestamp and the
/// raw body, the status can be asked for, and refunds are idempotent. It only exists in Development and Testing (PAY-15). In memory by design:
/// a restart forgets its sessions, which is acceptable for a fake.
/// </summary>
public sealed class FakeGateway(IOptions<OrdersOptions> options, TimeProvider clock) : IPaymentGateway
{
    public const string SignatureHeader = "X-Fake-Signature";

    /// <summary>How old a signed timestamp may be before the callback is refused as a replay (PAY-12).</summary>
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, FakeSession> _byRef = new();
    private readonly ConcurrentDictionary<string, string> _byAttempt = new();
    private readonly ConcurrentDictionary<string, RefundResult> _refunds = new();
    private int _calls;

    public string Provider => "fake";

    public bool IsConfigured => true;

    public FakeGatewayMode Mode { get; set; } = FakeGatewayMode.Normal;

    /// <summary>The gateway sessions opened so far (for tests and the demo page).</summary>
    public IReadOnlyCollection<FakeSession> Sessions => [.. _byRef.Values];

    /// <summary>How many refunds actually happened (a repeated idempotency key counts once, PAY-16).</summary>
    public int RefundCount => _refunds.Count;

    /// <summary>How many times a session was asked for, including repeats of the same attempt.</summary>
    public int SessionCreateCalls => Volatile.Read(ref _calls);

    private byte[] Secret => Encoding.UTF8.GetBytes(options.Value.FakeGatewayWebhookSecret.Length >= 16 ? options.Value.FakeGatewayWebhookSecret : "development-only-fake-gateway-secret");

    public Task<GatewaySession> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken)
    {
        ThrowIfDown();
        Interlocked.Increment(ref _calls);
        var gatewayRef = "fk_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.AttemptReference)))[..24].ToLowerInvariant();

        // The same attempt reference is the same session: asking twice (a retry after a timeout) never makes a second charge possible.
        _byAttempt.TryAdd(request.AttemptReference, gatewayRef);
        var session = _byRef.GetOrAdd(gatewayRef, _ => new FakeSession(gatewayRef, request.AttemptReference, request.OrderNumber, request.Amount, request.MethodCode, request.ReturnUrl));
        return Task.FromResult(new GatewaySession(session.GatewayRef, $"/customer-api/orders/payments/fake/{session.GatewayRef}"));
    }

    public CallbackVerification VerifyCallback(byte[] rawBody, IReadOnlyDictionary<string, string> headers, DateTimeOffset now)
    {
        // 1. The signature, over the exact bytes received and before anything is read from them (SEC-10).
        if (!headers.TryGetValue(SignatureHeader, out var header) || string.IsNullOrWhiteSpace(header))
        {
            return CallbackVerification.Reject(CallbackRejection.MissingSignature);
        }

        if (!TryParseHeader(header, out var timestamp, out var mac))
        {
            return CallbackVerification.Reject(CallbackRejection.BadSignature);
        }

        var expected = Sign(timestamp, rawBody);
        if (!CryptographicOperations.FixedTimeEquals(mac, expected))
        {
            return CallbackVerification.Reject(CallbackRejection.BadSignature);
        }

        // 2. A signed message that is too old (or from the future) is a replay (PAY-12).
        var signedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if ((now - signedAt).Duration() > Tolerance)
        {
            return CallbackVerification.Reject(CallbackRejection.StaleTimestamp);
        }

        // 3. Only now is the body read.
        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;
            var outcome = root.GetProperty("outcome").GetString() switch
            {
                "paid" => GatewayOutcome.Paid,
                "failed" => GatewayOutcome.Failed,
                _ => throw new FormatException("Unknown outcome."),
            };
            return CallbackVerification.Accept(new GatewayEvent(
                root.GetProperty("eventId").GetString() ?? string.Empty,
                root.GetProperty("gatewayRef").GetString() ?? string.Empty,
                outcome,
                root.GetProperty("amount").GetDecimal(),
                root.GetProperty("occurredAt").GetDateTimeOffset()));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return CallbackVerification.Reject(CallbackRejection.Malformed);
        }
    }

    public Task<GatewayStatus> GetStatusAsync(string gatewayRef, CancellationToken cancellationToken)
    {
        ThrowIfDown();
        return Task.FromResult(_byRef.TryGetValue(gatewayRef, out var session)
            ? new GatewayStatus(session.Status, session.Status == GatewayStatusKind.Paid ? session.Amount : null)
            : new GatewayStatus(GatewayStatusKind.Unknown, null));
    }

    public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken)
    {
        ThrowIfDown();
        var result = _refunds.GetOrAdd(request.IdempotencyKey, key => new RefundResult(true, "rf_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16].ToLowerInvariant()));
        return Task.FromResult(result);
    }

    /// <summary>Sets what the gateway will say about a session when asked (what the demo page's buttons do).</summary>
    public bool SetOutcome(string gatewayRef, GatewayOutcome outcome)
    {
        if (!_byRef.TryGetValue(gatewayRef, out var session))
        {
            return false;
        }

        session.Status = outcome == GatewayOutcome.Paid ? GatewayStatusKind.Paid : GatewayStatusKind.Failed;
        return true;
    }

    public FakeSession? Find(string gatewayRef) => _byRef.TryGetValue(gatewayRef, out var session) ? session : null;

    /// <summary>
    /// Builds a signed callback exactly as the gateway would send it: the raw body and the signature header. Tests and the demo page use it;
    /// they can override the amount, event id and time to make a bad one.
    /// </summary>
    public (byte[] Body, Dictionary<string, string> Headers) BuildCallback(string gatewayRef, GatewayOutcome outcome, decimal? amount = null, string? eventId = null, DateTimeOffset? at = null)
    {
        var time = at ?? clock.GetUtcNow();
        var sessionAmount = _byRef.TryGetValue(gatewayRef, out var session) ? session.Amount : 0m;
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            eventId = eventId ?? Guid.NewGuid().ToString("N"),
            gatewayRef,
            outcome = outcome == GatewayOutcome.Paid ? "paid" : "failed",
            amount = amount ?? sessionAmount,
            occurredAt = time,
        }));
        var timestamp = time.ToUnixTimeSeconds();
        var header = $"t={timestamp},v1={Convert.ToHexString(Sign(timestamp, body)).ToLowerInvariant()}";
        return (body, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [SignatureHeader] = header });
    }

    private byte[] Sign(long timestamp, byte[] body)
    {
        using var hmac = new HMACSHA256(Secret);
        return hmac.ComputeHash([.. Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + "."), .. body]);
    }

    private static bool TryParseHeader(string header, out long timestamp, out byte[] mac)
    {
        timestamp = 0;
        mac = [];
        string? t = null, v1 = null;
        foreach (var part in header.Split(','))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2)
            {
                return false;
            }

            if (pair[0] == "t")
            {
                t = pair[1];
            }
            else if (pair[0] == "v1")
            {
                v1 = pair[1];
            }
        }

        if (!long.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out timestamp) || v1 is null || v1.Length != 64)
        {
            return false;
        }

        try
        {
            mac = Convert.FromHexString(v1);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void ThrowIfDown()
    {
        if (Mode == FakeGatewayMode.Down)
        {
            throw new GatewayUnavailableException("The fake gateway is down.");
        }
    }
}
