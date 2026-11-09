using Romp.Modules.Orders.Domain.Payments;

namespace Romp.Modules.Orders.Application.Payments;

/// <summary>The gateway could not be reached, timed out, answered with a server error, or the circuit is open. Counted by the circuit breaker (SEC-21).</summary>
public sealed class GatewayUnavailableException : Exception
{
    public GatewayUnavailableException()
    {
    }

    public GatewayUnavailableException(string message)
        : base(message)
    {
    }

    public GatewayUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>What the gateway needs to open a hosted payment page. <see cref="AttemptReference"/> is stable per order and attempt number, so asking twice gives the same session (no double charge).</summary>
public sealed record CreateSessionRequest(string OrderNumber, string AttemptReference, decimal Amount, string MethodCode, string ReturnUrl);

public sealed record GatewaySession(string GatewayRef, string RedirectUrl);

/// <summary>One verified message from the gateway, reduced to what Orders needs. Nothing about the payer, card or wallet is in it (PAY-9).</summary>
public sealed record GatewayEvent(string EventId, string GatewayRef, GatewayOutcome Outcome, decimal Amount, DateTimeOffset OccurredAt);

public enum CallbackRejection
{
    None,
    MissingSignature,
    BadSignature,
    StaleTimestamp,
    Malformed,
}

public sealed record CallbackVerification(bool Verified, CallbackRejection Rejection, GatewayEvent? Event)
{
    public static CallbackVerification Accept(GatewayEvent gatewayEvent) => new(true, CallbackRejection.None, gatewayEvent);

    public static CallbackVerification Reject(CallbackRejection why) => new(false, why, null);
}

public enum GatewayStatusKind
{
    /// <summary>The gateway has no such session.</summary>
    Unknown,

    Pending,
    Paid,
    Failed,
}

public sealed record GatewayStatus(GatewayStatusKind Kind, decimal? Amount);

/// <summary>A refund of an earlier charge. <see cref="IdempotencyKey"/> makes a repeated request refund once (PAY-16). Nothing in S12b calls it; S17 does.</summary>
public sealed record RefundRequest(string GatewayRef, decimal Amount, string IdempotencyKey);

public sealed record RefundResult(bool Succeeded, string? RefundRef);

/// <summary>
/// The payment gateway as Orders sees it (ADR 0027). One implementation per provider; a fake ships first and drives every test and the local
/// stack. Hosted payment page only: no card data, wallet PIN or OTP ever reaches Romp (SAQ-A). Every call that leaves the process may throw
/// <see cref="GatewayUnavailableException"/> and is wrapped by the circuit breaker (SEC-21).
/// </summary>
public interface IPaymentGateway
{
    /// <summary>The route segment and stored name of this provider (<c>fake</c>).</summary>
    string Provider { get; }

    /// <summary>False for the placeholder used until a gateway is switched on: online methods stay off.</summary>
    bool IsConfigured { get; }

    Task<GatewaySession> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the signature over the raw bytes first, then (and only then) reads them (SEC-10). A bad, missing or stale signature is a rejection,
    /// never an exception. Pure and local: no network.
    /// </summary>
    CallbackVerification VerifyCallback(byte[] rawBody, IReadOnlyDictionary<string, string> headers, DateTimeOffset now);

    Task<GatewayStatus> GetStatusAsync(string gatewayRef, CancellationToken cancellationToken);

    Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken);
}

/// <summary>Whether online payment can be offered right now: a gateway is switched on and its circuit is not open (PAY-7, PAY-14).</summary>
public interface IGatewayAvailability
{
    bool IsAvailable { get; }

    /// <summary>True when a gateway is configured but the circuit is open: the page says only Cash on Delivery is available for now.</summary>
    bool IsPaused { get; }
}

/// <summary>Makes and reads the signed token that lets a shopper start or follow the payment of exactly one order (no account needed for guests, SEC-05).</summary>
public interface IPaymentLinks
{
    string Create(string orderNumber, DateTimeOffset now);

    string? TryRead(string? token, DateTimeOffset now);

    /// <summary>Where the gateway sends the shopper's browser afterwards. Only a display hint: never what decides the order state (PAY-10).</summary>
    string ReturnUrl(string token);
}
