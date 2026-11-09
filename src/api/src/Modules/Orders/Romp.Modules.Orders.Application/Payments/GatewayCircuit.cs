using Microsoft.Extensions.Options;
using Romp.Modules.Orders.Application.Checkout;

namespace Romp.Modules.Orders.Application.Payments;

/// <summary>
/// A circuit breaker for the payment gateway (SEC-21, PAY-7). It opens after <c>threshold</c> failures inside <c>window</c>, stays open for
/// <c>openFor</c>, then lets one trial call through (half-open): a success closes it, a failure opens it again. It uses the injected clock, so
/// it is tested without waiting. Thread safe.
/// </summary>
public sealed class GatewayCircuit(TimeProvider clock, int threshold, TimeSpan window, TimeSpan openFor)
{
    private readonly object _gate = new();
    private readonly Queue<DateTimeOffset> _failures = new();
    private DateTimeOffset? _openedAt;
    private bool _trialInFlight;

    /// <summary>True while calls are refused: open and not yet time for a trial.</summary>
    public bool IsOpen
    {
        get
        {
            lock (_gate)
            {
                return _openedAt is { } opened && clock.GetUtcNow() < opened + openFor;
            }
        }
    }

    /// <summary>Asks to make a call. False while open. After the open period exactly one caller gets the trial; others are refused until it reports.</summary>
    public bool TryAcquire()
    {
        lock (_gate)
        {
            if (_openedAt is not { } opened)
            {
                return true;
            }

            if (clock.GetUtcNow() < opened + openFor || _trialInFlight)
            {
                return false;
            }

            _trialInFlight = true;
            return true;
        }
    }

    /// <summary>The caller gave up before the call finished: say nothing about the gateway, but never leave a half-open trial stuck.</summary>
    public void ReleaseTrial()
    {
        lock (_gate)
        {
            _trialInFlight = false;
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _failures.Clear();
            _openedAt = null;
            _trialInFlight = false;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            if (_trialInFlight)
            {
                _trialInFlight = false;
                _openedAt = now;
                return;
            }

            _failures.Enqueue(now);
            while (_failures.Count > 0 && now - _failures.Peek() > window)
            {
                _failures.Dequeue();
            }

            if (_failures.Count >= threshold)
            {
                _openedAt = now;
                _failures.Clear();
            }
        }
    }
}

/// <summary>
/// Puts every network call to the gateway behind a timeout and the <see cref="GatewayCircuit"/> (SEC-21, Q-PAY-5: open after 5 failures in a
/// minute, a trial after 30 seconds). Local work (verifying a callback) is not wrapped: it cannot fail the way a network call does.
/// </summary>
public sealed class ResilientPaymentGateway(IPaymentGateway inner, GatewayCircuit circuit, IOptions<OrdersOptions> options) : IPaymentGateway, IGatewayAvailability
{
    public string Provider => inner.Provider;

    public bool IsConfigured => inner.IsConfigured;

    public bool IsAvailable => inner.IsConfigured && !circuit.IsOpen;

    public bool IsPaused => inner.IsConfigured && circuit.IsOpen;

    public CallbackVerification VerifyCallback(byte[] rawBody, IReadOnlyDictionary<string, string> headers, DateTimeOffset now) =>
        inner.VerifyCallback(rawBody, headers, now);

    public Task<GatewaySession> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken) =>
        CallAsync(token => inner.CreateSessionAsync(request, token), cancellationToken);

    public Task<GatewayStatus> GetStatusAsync(string gatewayRef, CancellationToken cancellationToken) =>
        CallAsync(token => inner.GetStatusAsync(gatewayRef, token), cancellationToken);

    public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken) =>
        CallAsync(token => inner.RefundAsync(request, token), cancellationToken);

    private async Task<T> CallAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        if (!inner.IsConfigured)
        {
            throw new GatewayUnavailableException("No payment gateway is switched on.");
        }

        if (!circuit.TryAcquire())
        {
            throw new GatewayUnavailableException("The payment gateway is paused after repeated failures.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.GatewayTimeoutSeconds));
        try
        {
            var result = await call(timeout.Token);
            circuit.RecordSuccess();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up (a shopper closed the page): that says nothing about the gateway.
            circuit.ReleaseTrial();
            throw;
        }
        catch (Exception ex) when (ex is GatewayUnavailableException or OperationCanceledException or HttpRequestException or TimeoutException)
        {
            circuit.RecordFailure();
            throw ex as GatewayUnavailableException ?? new GatewayUnavailableException("The payment gateway did not answer.", ex);
        }
    }
}

/// <summary>The placeholder used until a gateway is switched on: not configured, so online methods are never offered and every call says so.</summary>
public sealed class NoGateway : IPaymentGateway
{
    public string Provider => "none";

    public bool IsConfigured => false;

    public Task<GatewaySession> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken) =>
        throw new GatewayUnavailableException("No payment gateway is switched on.");

    public CallbackVerification VerifyCallback(byte[] rawBody, IReadOnlyDictionary<string, string> headers, DateTimeOffset now) =>
        CallbackVerification.Reject(CallbackRejection.BadSignature);

    public Task<GatewayStatus> GetStatusAsync(string gatewayRef, CancellationToken cancellationToken) =>
        throw new GatewayUnavailableException("No payment gateway is switched on.");

    public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken) =>
        throw new GatewayUnavailableException("No payment gateway is switched on.");
}
