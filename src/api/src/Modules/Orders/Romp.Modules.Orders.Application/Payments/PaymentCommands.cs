using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;

namespace Romp.Modules.Orders.Application.Payments;

// ---------------------------------------------------------------------------------------------------------------------------------------
// Start (Y3): open the hosted payment page, or hand back the one that is already open. Never two sessions for one try (no double charge).
// ---------------------------------------------------------------------------------------------------------------------------------------

/// <summary>
/// Starts (or restarts after a decline) the online payment of the order named by the signed token (PAY-2, PAY-3). The gateway call happens
/// outside any database transaction and is idempotent on the attempt reference, so a retry after a timeout reaches the same session.
/// </summary>
public sealed record StartOnlinePaymentCommand(string? Token) : IRequest<StartPaymentResult>;

public enum StartPaymentKind
{
    /// <summary>A new gateway session was opened.</summary>
    Started,

    /// <summary>A session was already open; the same one is returned.</summary>
    Reused,

    AlreadyPaid,
    InvalidLink,
    NotOnline,
    OrderClosed,
    WindowClosed,
    TooManyAttempts,
    GatewayUnavailable,
    UnderReview,
}

public sealed record StartPaymentResult(StartPaymentKind Kind, string? RedirectUrl = null);

public sealed class StartOnlinePaymentCommandHandler(
    IOrdersDbContext dbContext,
    IPaymentGateway gateway,
    IGatewayAvailability availability,
    IPaymentLinks links,
    TimeProvider clock,
    IOptions<OrdersOptions> options) : IRequestHandler<StartOnlinePaymentCommand, StartPaymentResult>
{
    public async Task<StartPaymentResult> Handle(StartOnlinePaymentCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var number = links.TryRead(request.Token, now);
        if (number is null)
        {
            return new StartPaymentResult(StartPaymentKind.InvalidLink);
        }

        var order = await dbContext.Orders.AsNoTracking()
            .Where(o => o.OrderNumber == number)
            .Select(o => new { o.Id, o.OrderNumber, o.StatusId })
            .FirstOrDefaultAsync(cancellationToken);
        if (order is null)
        {
            return new StartPaymentResult(StartPaymentKind.InvalidLink);
        }

        var payment = await dbContext.Payments.Include(p => p.Attempts).SingleOrDefaultAsync(p => p.OrderId == order.Id, cancellationToken);
        if (payment is null || PaymentMethods.IsCashOnDelivery(payment.MethodId))
        {
            return new StartPaymentResult(StartPaymentKind.NotOnline);
        }

        if (order.StatusId == OrderStatuses.Cancelled)
        {
            return new StartPaymentResult(StartPaymentKind.OrderClosed);
        }

        var max = options.Value.PaymentMaxAttempts;
        switch (payment.DecideStart(now, max))
        {
            case StartDecision.AlreadyPaid:
                return new StartPaymentResult(StartPaymentKind.AlreadyPaid);
            case StartDecision.ReuseOpen:
                return new StartPaymentResult(StartPaymentKind.Reused, payment.OpenAttempt!.RedirectUrl);
            case StartDecision.WindowClosed:
                return new StartPaymentResult(StartPaymentKind.WindowClosed);
            case StartDecision.TooManyAttempts:
                return new StartPaymentResult(StartPaymentKind.TooManyAttempts);
            case StartDecision.NotPayable:
                return new StartPaymentResult(payment.StatusId == PaymentStatuses.Expired ? StartPaymentKind.OrderClosed : StartPaymentKind.UnderReview);
            case StartDecision.NotOnline:
                return new StartPaymentResult(StartPaymentKind.NotOnline);
        }

        if (!availability.IsAvailable)
        {
            return new StartPaymentResult(StartPaymentKind.GatewayUnavailable);
        }

        GatewaySession session;
        try
        {
            session = await gateway.CreateSessionAsync(
                new CreateSessionRequest(order.OrderNumber, $"{order.OrderNumber}-A{payment.AttemptCount + 1}", payment.Amount, PaymentMethods.CodeOf(payment.MethodId), links.ReturnUrl(request.Token!)),
                cancellationToken);
        }
        catch (GatewayUnavailableException)
        {
            // Nothing was recorded, so the shopper simply tries again: the same attempt reference reaches the same gateway session.
            return new StartPaymentResult(StartPaymentKind.GatewayUnavailable);
        }

        payment.BeginAttempt(session.GatewayRef, session.RedirectUrl, now, max);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two starts raced: the database allows one attempt number once. The loser hands back what the winner saved.
            dbContext.DiscardChanges();
            var winner = await dbContext.Payments.AsNoTracking().Include(p => p.Attempts).SingleAsync(p => p.OrderId == order.Id, cancellationToken);
            return winner.OpenAttempt is { } open
                ? new StartPaymentResult(StartPaymentKind.Reused, open.RedirectUrl)
                : throw new InvalidOperationException("A payment attempt could not be saved.");
        }

        return new StartPaymentResult(StartPaymentKind.Started, session.RedirectUrl);
    }
}

// ---------------------------------------------------------------------------------------------------------------------------------------
// Status (Y3): what the return page shows. Only the stored state decides; the browser coming back proves nothing (PAY-10).
// ---------------------------------------------------------------------------------------------------------------------------------------

public sealed record GetPaymentStatusQuery(string? Token) : IRequest<PaymentStatusDto?>;

/// <summary><see cref="State"/> is one of <c>confirming</c>, <c>paid</c>, <c>failed</c>, <c>expired</c>, <c>cancelled</c>, <c>review</c>, <c>cod</c>.</summary>
public sealed record PaymentStatusDto(string State, string OrderNumber, decimal Total, bool CanRetry, string MethodCode);

public sealed class GetPaymentStatusQueryHandler(IOrdersDbContext dbContext, IPaymentLinks links, TimeProvider clock, IOptions<OrdersOptions> options)
    : IRequestHandler<GetPaymentStatusQuery, PaymentStatusDto?>
{
    public async Task<PaymentStatusDto?> Handle(GetPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var number = links.TryRead(request.Token, now);
        if (number is null)
        {
            return null;
        }

        var order = await dbContext.Orders.AsNoTracking()
            .Where(o => o.OrderNumber == number)
            .Select(o => new { o.Id, o.OrderNumber, o.StatusId, o.Total })
            .FirstOrDefaultAsync(cancellationToken);
        if (order is null)
        {
            return null;
        }

        var payment = await dbContext.Payments.AsNoTracking().Include(p => p.Attempts).SingleOrDefaultAsync(p => p.OrderId == order.Id, cancellationToken);
        if (payment is null)
        {
            return null;
        }

        var method = PaymentMethods.CodeOf(payment.MethodId);
        if (PaymentMethods.IsCashOnDelivery(payment.MethodId))
        {
            return new PaymentStatusDto("cod", order.OrderNumber, order.Total, false, method);
        }

        var canRetry = order.StatusId == OrderStatuses.PaymentPending && payment.DecideStart(now, options.Value.PaymentMaxAttempts) == StartDecision.StartNew;
        var state = (payment.StatusId, order.StatusId) switch
        {
            (PaymentStatuses.Paid, _) => "paid",
            (PaymentStatuses.InReview, _) => "review",
            (PaymentStatuses.Expired, _) => "expired",
            (_, OrderStatuses.Cancelled) when payment.StatusId == PaymentStatuses.Failed => "failed",
            (_, OrderStatuses.Cancelled) => "cancelled",
            (PaymentStatuses.Failed, _) => "failed",
            _ => "confirming",
        };
        return new PaymentStatusDto(state, order.OrderNumber, order.Total, canRetry, method);
    }
}

// ---------------------------------------------------------------------------------------------------------------------------------------
// Webhook (Y4): signature first, then store, then apply once.
// ---------------------------------------------------------------------------------------------------------------------------------------

/// <summary>A message received on the payment webhook, exactly as it arrived. The signature is checked over these bytes before anything is read (SEC-10).</summary>
public sealed record HandlePaymentWebhookCommand(string Provider, byte[] Body, IReadOnlyDictionary<string, string> Headers) : IRequest<WebhookResult>, IOrdersCommand;

public enum WebhookResultKind
{
    /// <summary>Verified and recorded (applied, flagged, or about nothing we know): the gateway need not retry.</summary>
    Accepted,

    /// <summary>Missing, bad or stale signature, or unreadable: rejected and recorded as such (PAY-4, PAY-12).</summary>
    Rejected,

    /// <summary>The same provider event was already handled: nothing changed (PAY-5).</summary>
    Duplicate,

    /// <summary>No such provider is switched on.</summary>
    UnknownProvider,
}

public sealed record WebhookResult(WebhookResultKind Kind, CallbackRejection Rejection = CallbackRejection.None);

public sealed partial class HandlePaymentWebhookCommandHandler(
    IOrdersDbContext dbContext,
    IPaymentGateway gateway,
    PaymentSettler settler,
    WebhookRejectionBudget rejections,
    TimeProvider clock,
    ILogger<HandlePaymentWebhookCommandHandler> logger) : IRequestHandler<HandlePaymentWebhookCommand, WebhookResult>
{
    public const int MaxEventIdLength = 100;

    public async Task<WebhookResult> Handle(HandlePaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        // The route names a provider; only the one that is switched on is answered. Nothing from the path is stored.
        if (!gateway.IsConfigured || !string.Equals(request.Provider, gateway.Provider, StringComparison.OrdinalIgnoreCase))
        {
            return new WebhookResult(WebhookResultKind.UnknownProvider);
        }

        var provider = gateway.Provider;
        var now = clock.GetUtcNow();
        var hash = Convert.ToHexString(SHA256.HashData(request.Body)).ToLowerInvariant();

        var check = gateway.VerifyCallback(request.Body, request.Headers, now);
        if (!check.Verified || check.Event is not { } gatewayEvent || gatewayEvent.EventId.Length is 0 or > MaxEventIdLength || gatewayEvent.GatewayRef.Length is 0 or > PaymentAttempt.MaxReferenceLength)
        {
            var rejection = check.Verified ? CallbackRejection.Malformed : check.Rejection;
            // Kept as a security record (PAY-4), within a budget so a flood cannot fill the table; every rejection is logged either way.
            var stored = rejections.TryTake();
            if (stored)
            {
                dbContext.PaymentWebhooks.Add(new PaymentWebhook(provider, null, false, hash, WebhookOutcomes.Rejected, null, null, now));
            }

            LogRejected(rejection.ToString(), stored);
            return new WebhookResult(WebhookResultKind.Rejected, rejection);
        }

        if (await AlreadyHandledAsync(provider, gatewayEvent.EventId, cancellationToken))
        {
            return new WebhookResult(WebhookResultKind.Duplicate);
        }

        var paymentId = await dbContext.PaymentAttempts.AsNoTracking()
            .Where(a => a.GatewayRef == gatewayEvent.GatewayRef)
            .Select(a => (long?)a.PaymentId)
            .FirstOrDefaultAsync(cancellationToken);
        if (paymentId is null)
        {
            // A signed message about something we never started: kept for the record, changes nothing.
            dbContext.PaymentWebhooks.Add(new PaymentWebhook(provider, gatewayEvent.EventId, true, hash, WebhookOutcomes.Unmatched, gatewayEvent.GatewayRef, null, now));
            return new WebhookResult(WebhookResultKind.Accepted);
        }

        // One message about this payment at a time. A twin that waited here finds the first one's record below and stops.
        var payment = await settler.LoadLockedAsync(paymentId.Value, cancellationToken);
        if (await AlreadyHandledAsync(provider, gatewayEvent.EventId, cancellationToken))
        {
            return new WebhookResult(WebhookResultKind.Duplicate);
        }

        var settlement = await settler.ApplyAsync(payment, gatewayEvent.GatewayRef, gatewayEvent.Outcome, gatewayEvent.Amount, cancellationToken);
        var outcome = settlement switch
        {
            Settlement.Paid or Settlement.FailureRecorded => WebhookOutcomes.Applied,
            Settlement.AlreadyApplied => WebhookOutcomes.Ignored,
            Settlement.Unmatched => WebhookOutcomes.Unmatched,
            _ => WebhookOutcomes.Flagged,
        };
        dbContext.PaymentWebhooks.Add(new PaymentWebhook(provider, gatewayEvent.EventId, true, hash, outcome, gatewayEvent.GatewayRef, payment.Id, now));
        if (outcome == WebhookOutcomes.Flagged)
        {
            LogFlagged(settlement.ToString());
        }

        return new WebhookResult(WebhookResultKind.Accepted);
    }

    private Task<bool> AlreadyHandledAsync(string provider, string eventId, CancellationToken cancellationToken) =>
        dbContext.PaymentWebhooks.AsNoTracking().AnyAsync(w => w.Provider == provider && w.EventId == eventId, cancellationToken);

    // SEC-26: these are security events and are logged apart from routine errors. Nothing about the payer is in them (SEC-25).
    [LoggerMessage(EventId = 7001, Level = LogLevel.Warning, Message = "SecurityEvent PaymentWebhookRejected: {Reason} (stored: {Stored})")]
    private partial void LogRejected(string reason, bool stored);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Warning, Message = "SecurityEvent PaymentFlaggedForReview: {Settlement}")]
    private partial void LogFlagged(string settlement);
}

// ---------------------------------------------------------------------------------------------------------------------------------------
// Apply a status the gateway reported when asked (reconciliation, expiry). Same path as the webhook.
// ---------------------------------------------------------------------------------------------------------------------------------------

public sealed record ApplyGatewayStatusCommand(string GatewayRef, GatewayOutcome Outcome, decimal Amount) : IRequest<Settlement>, IOrdersCommand;

public sealed class ApplyGatewayStatusCommandHandler(IOrdersDbContext dbContext, PaymentSettler settler) : IRequestHandler<ApplyGatewayStatusCommand, Settlement>
{
    public async Task<Settlement> Handle(ApplyGatewayStatusCommand request, CancellationToken cancellationToken)
    {
        var paymentId = await dbContext.PaymentAttempts.AsNoTracking()
            .Where(a => a.GatewayRef == request.GatewayRef)
            .Select(a => (long?)a.PaymentId)
            .FirstOrDefaultAsync(cancellationToken);
        if (paymentId is null)
        {
            return Settlement.Unmatched;
        }

        var payment = await settler.LoadLockedAsync(paymentId.Value, cancellationToken);
        return await settler.ApplyAsync(payment, request.GatewayRef, request.Outcome, request.Amount, cancellationToken);
    }
}

