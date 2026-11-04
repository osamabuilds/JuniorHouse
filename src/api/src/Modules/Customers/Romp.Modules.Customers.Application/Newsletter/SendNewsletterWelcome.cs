using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Newsletter;

namespace Romp.Modules.Customers.Application.Newsletter;

/// <summary>Sends the welcome email (ADR 0022). Infrastructure renders and sends it; the unsubscribe token is made here so the signing rule has one home.</summary>
public interface INewsletterMailer
{
    Task SendWelcomeAsync(string toEmail, string unsubscribeToken, CancellationToken cancellationToken);
}

/// <summary>
/// Sends the one welcome for a subscription (NLS-3, NLS-6). It reads the record at send time, so an address that unsubscribed before the
/// message went out gets nothing, and a record that already had its welcome is not mailed again. A send failure throws: the transaction
/// rolls back, nothing is marked, and the outbox dispatcher retries the message (NFR-FT-08).
/// </summary>
public sealed record SendNewsletterWelcomeCommand(long SubscriptionId) : IRequest<bool>, ICustomerCommand;

public sealed class SendNewsletterWelcomeCommandHandler(
    ICustomerDbContext dbContext, INewsletterMailer mailer, IOptions<CustomersOptions> options, TimeProvider clock)
    : IRequestHandler<SendNewsletterWelcomeCommand, bool>
{
    public async Task<bool> Handle(SendNewsletterWelcomeCommand request, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Newsletter.FirstOrDefaultAsync(s => s.Id == request.SubscriptionId, cancellationToken);
        if (subscription is null || !subscription.IsSubscribed || subscription.WelcomeSentAt is not null)
        {
            return false;
        }

        var token = UnsubscribeToken.Create(subscription.Id, options.Value.LinkSigningKey);
        await mailer.SendWelcomeAsync(subscription.Email, token, cancellationToken);
        subscription.MarkWelcomeSent(clock.GetUtcNow());
        return true;
    }
}
