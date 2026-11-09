using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;

namespace Romp.Modules.Orders.Application.Payments;

/// <summary>
/// The one path by which a gateway result changes an order (ADR 0027, point 7): the webhook, the reconciliation job and the expiry job all end
/// here, so there is no second way to mark an order paid. The caller holds the payment row lock inside the Orders transaction
/// (<see cref="IOrdersDbContext.LockPaymentAsync"/>), which is what makes a repeated or racing message count once.
/// </summary>
public sealed class PaymentSettler(IOrdersDbContext dbContext, TimeProvider clock, IOptions<OrdersOptions> options)
{
    /// <summary>Locks the payment row, then loads it with its attempts, so what is read is what the previous holder committed.</summary>
    public async Task<Payment> LoadLockedAsync(long paymentId, CancellationToken cancellationToken)
    {
        await dbContext.LockPaymentAsync(paymentId, cancellationToken);
        return await dbContext.Payments.Include(p => p.Attempts).SingleAsync(p => p.Id == paymentId, cancellationToken);
    }

    /// <summary>Applies one gateway result to a locked, loaded payment and the order it belongs to.</summary>
    public async Task<Settlement> ApplyAsync(Payment payment, string gatewayRef, GatewayOutcome outcome, decimal amount, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var order = await dbContext.Orders.Include(o => o.History).SingleAsync(o => o.Id == payment.OrderId, cancellationToken);
        var result = payment.Settle(gatewayRef, outcome, amount, order.IsAwaitingPayment, now);
        switch (result)
        {
            case Settlement.Paid:
                order.MarkPaid(now);
                break;

            case Settlement.FailureRecorded when payment.StatusId == PaymentStatuses.Failed && payment.AttemptCount >= options.Value.PaymentMaxAttempts:
                // Every try has been used: the order is cancelled and stock and discount come back (the cancellation event does that).
                order.Cancel(CancelReasons.PaymentFailed, ActorKinds.Gateway, null, "Payment failed", wasPicked: false, now);
                break;

            case Settlement.AmountMismatch:
                order.FlagPaymentSuspect(PaymentReviewReasons.CodeOf(PaymentReviewReasons.AmountMismatch));
                break;

            case Settlement.NotPayable:
                order.FlagPaymentSuspect(PaymentReviewReasons.CodeOf(PaymentReviewReasons.OrderNotPayable));
                break;

            case Settlement.DuplicateCharge:
                order.FlagPaymentSuspect(PaymentReviewReasons.CodeOf(PaymentReviewReasons.DuplicateCharge));
                break;

            default:
                break;
        }

        return result;
    }
}
