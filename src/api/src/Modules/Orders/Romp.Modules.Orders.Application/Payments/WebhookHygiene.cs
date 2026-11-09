using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Application.Checkout;
using Romp.Modules.Orders.Domain.Payments;

namespace Romp.Modules.Orders.Application.Payments;

/// <summary>
/// Keeps a flood of forged callbacks from filling the database (SEC-10, SEC-26, NFR-04). A rejected message is kept as a security record
/// (PAY-4), but only up to <c>Orders:RejectedWebhooksStoredPerMinute</c> a minute across the whole host; the rest are counted in the security
/// log alone. A genuine gateway message is never affected: only rejections draw on this budget.
/// </summary>
public sealed class WebhookRejectionBudget(TimeProvider clock, IOptions<OrdersOptions> options)
{
    private readonly object _gate = new();
    private long _minute = -1;
    private int _taken;

    /// <summary>True when one more rejected message may be stored in this minute.</summary>
    public bool TryTake()
    {
        var minute = clock.GetUtcNow().ToUnixTimeSeconds() / 60;
        lock (_gate)
        {
            if (minute != _minute)
            {
                _minute = minute;
                _taken = 0;
            }

            if (_taken >= options.Value.RejectedWebhooksStoredPerMinute)
            {
                return false;
            }

            _taken++;
            return true;
        }
    }
}

/// <summary>Deletes old rejected-callback records (they are only a security trail and are not needed for ever). Verified messages are kept.</summary>
public sealed record PurgePaymentWebhooksCommand : IRequest<int>;

public sealed class PurgePaymentWebhooksCommandHandler(IOrdersDbContext dbContext, TimeProvider clock, IOptions<OrdersOptions> options) : IRequestHandler<PurgePaymentWebhooksCommand, int>
{
    public async Task<int> Handle(PurgePaymentWebhooksCommand request, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - TimeSpan.FromDays(options.Value.RejectedWebhookRetentionDays);
        return await dbContext.PaymentWebhooks
            .Where(w => w.OutcomeId == WebhookOutcomes.Rejected && w.ReceivedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
