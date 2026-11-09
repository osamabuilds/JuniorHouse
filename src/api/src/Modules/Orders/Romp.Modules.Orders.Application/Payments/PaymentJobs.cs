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
// Reconcile (Y6): a charge whose callback was lost is found by asking the gateway, and completed once (FR-30, PAY-6).
// ---------------------------------------------------------------------------------------------------------------------------------------

public sealed record ReconcilePaymentsCommand : IRequest<ReconcileResult>;

public sealed record ReconcileResult(int Checked, int Settled, bool GatewayUnreachable);

/// <summary>
/// Asks the gateway about every attempt still unanswered after <c>ReconcileAfterMinutes</c> (for up to <c>ReconcileMaxAgeHours</c>) and applies
/// what it says through the same path as the webhook, so a lost callback and a late one can never both count (PAY-6). Works through all that
/// are due, a batch at a time. When the gateway cannot answer it stops and tries again on the next run: nothing is changed on a guess.
/// </summary>
public sealed partial class ReconcilePaymentsCommandHandler(
    IOrdersDbContext dbContext,
    IPaymentGateway gateway,
    ISender sender,
    TimeProvider clock,
    IOptions<OrdersOptions> options,
    ILogger<ReconcilePaymentsCommandHandler> logger) : IRequestHandler<ReconcilePaymentsCommand, ReconcileResult>
{
    public const int BatchSize = 50;
    public const int MaxBatches = 20;

    public async Task<ReconcileResult> Handle(ReconcilePaymentsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var settings = options.Value;
        var olderThan = now - TimeSpan.FromMinutes(settings.ReconcileAfterMinutes);
        var newerThan = now - TimeSpan.FromHours(settings.ReconcileMaxAgeHours);

        int settled = 0, asked = 0;
        var done = new List<string>();
        for (var batch = 0; batch < MaxBatches; batch++)
        {
            var refs = await dbContext.PaymentAttempts.AsNoTracking()
                .Where(a => a.StatusId == PaymentStatuses.Pending && a.StartedAt <= olderThan && a.StartedAt >= newerThan && !done.Contains(a.GatewayRef))
                .OrderBy(a => a.StartedAt)
                .Select(a => a.GatewayRef)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (refs.Count == 0)
            {
                break;
            }

            done.AddRange(refs);
            foreach (var gatewayRef in refs)
            {
                GatewayStatus status;
                try
                {
                    status = await gateway.GetStatusAsync(gatewayRef, cancellationToken);
                }
                catch (GatewayUnavailableException)
                {
                    return new ReconcileResult(asked, settled, true);
                }

                asked++;
                if (status.Kind is GatewayStatusKind.Pending or GatewayStatusKind.Unknown)
                {
                    continue;
                }

                try
                {
                    var outcome = status.Kind == GatewayStatusKind.Paid ? GatewayOutcome.Paid : GatewayOutcome.Failed;
                    // A "paid" answer without an amount cannot be trusted to match the total: it is held for review, not confirmed (PAY-8).
                    var result = await sender.Send(new ApplyGatewayStatusCommand(gatewayRef, outcome, status.Amount ?? -1m), cancellationToken);
                    if (result is Settlement.Paid or Settlement.FailureRecorded)
                    {
                        settled++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    dbContext.DiscardChanges();
                    LogItemFailed(ex);
                }
            }
        }

        return new ReconcileResult(asked, settled, false);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Reconciling one payment failed; it will be tried again on the next run.")]
    private partial void LogItemFailed(Exception exception);
}

// ---------------------------------------------------------------------------------------------------------------------------------------
// Expire (Y8): an online order still unpaid when its window closes is cancelled and its stock and discount come back (PAY-11).
// ---------------------------------------------------------------------------------------------------------------------------------------

public sealed record ExpireUnpaidOrdersCommand : IRequest<ExpireResult>;

public sealed record ExpireResult(int Expired, int SettledInstead, int Skipped);

/// <summary>Expires one payment and cancels its order, under the payment row lock; does nothing if it was settled meanwhile or the window has not passed.</summary>
public sealed record ExpirePaymentCommand(long PaymentId) : IRequest<bool>, IOrdersCommand;

public sealed class ExpirePaymentCommandHandler(IOrdersDbContext dbContext, PaymentSettler settler, TimeProvider clock) : IRequestHandler<ExpirePaymentCommand, bool>
{
    public async Task<bool> Handle(ExpirePaymentCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var payment = await settler.LoadLockedAsync(request.PaymentId, cancellationToken);
        if (payment.WindowEndsAt is not { } end || now < end || !payment.Expire(now))
        {
            return false;
        }

        var order = await dbContext.Orders.Include(o => o.History).SingleAsync(o => o.Id == payment.OrderId, cancellationToken);
        order.Cancel(CancelReasons.PaymentExpired, ActorKinds.System, null, "Payment window passed", wasPicked: false, now);
        return true;
    }
}

/// <summary>
/// Cancels online orders whose payment window has passed. Before giving up on one it asks the gateway about every try still open: a paid
/// order is never cancelled (FR-30), and when the gateway cannot answer the order is left for the next run instead of cancelled on a guess.
/// </summary>
public sealed partial class ExpireUnpaidOrdersCommandHandler(
    IOrdersDbContext dbContext,
    IPaymentGateway gateway,
    ISender sender,
    TimeProvider clock,
    ILogger<ExpireUnpaidOrdersCommandHandler> logger) : IRequestHandler<ExpireUnpaidOrdersCommand, ExpireResult>
{
    public const int BatchSize = 50;
    public const int MaxBatches = 20;

    public async Task<ExpireResult> Handle(ExpireUnpaidOrdersCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        int expired = 0, settledInstead = 0, skipped = 0;
        var done = new List<long>();

        for (var batch = 0; batch < MaxBatches; batch++)
        {
            var due = await dbContext.Payments.AsNoTracking()
                .Where(p => (p.StatusId == PaymentStatuses.Initiated || p.StatusId == PaymentStatuses.Pending || p.StatusId == PaymentStatuses.Failed)
                            && p.WindowEndsAt != null && p.WindowEndsAt <= now && !done.Contains(p.Id))
                .OrderBy(p => p.WindowEndsAt)
                .Select(p => p.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (due.Count == 0)
            {
                break;
            }

            done.AddRange(due);
            foreach (var paymentId in due)
            {
                try
                {
                    switch (await ExpireOneAsync(paymentId, cancellationToken))
                    {
                        case ExpireStep.Expired:
                            expired++;
                            break;
                        case ExpireStep.SettledInstead:
                            settledInstead++;
                            break;
                        default:
                            skipped++;
                            break;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    dbContext.DiscardChanges();
                    skipped++;
                    LogItemFailed(ex);
                }
            }
        }

        return new ExpireResult(expired, settledInstead, skipped);
    }

    private enum ExpireStep
    {
        Expired,
        SettledInstead,
        Skipped,
    }

    private async Task<ExpireStep> ExpireOneAsync(long paymentId, CancellationToken cancellationToken)
    {
        var openRefs = await dbContext.PaymentAttempts.AsNoTracking()
            .Where(a => a.PaymentId == paymentId && a.StatusId == PaymentStatuses.Pending)
            .Select(a => a.GatewayRef)
            .ToListAsync(cancellationToken);

        var paidMeanwhile = false;
        foreach (var gatewayRef in openRefs)
        {
            try
            {
                var status = await gateway.GetStatusAsync(gatewayRef, cancellationToken);
                if (status.Kind == GatewayStatusKind.Paid)
                {
                    await sender.Send(new ApplyGatewayStatusCommand(gatewayRef, GatewayOutcome.Paid, status.Amount ?? -1m), cancellationToken);
                    paidMeanwhile = true;
                }
            }
            catch (GatewayUnavailableException)
            {
                // Cannot tell whether it was paid: leave it, ask again next run.
                return ExpireStep.Skipped;
            }
        }

        if (paidMeanwhile)
        {
            return ExpireStep.SettledInstead;
        }

        return await sender.Send(new ExpirePaymentCommand(paymentId), cancellationToken) ? ExpireStep.Expired : ExpireStep.Skipped;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Expiring one unpaid order failed; it will be tried again on the next run.")]
    private partial void LogItemFailed(Exception exception);
}
