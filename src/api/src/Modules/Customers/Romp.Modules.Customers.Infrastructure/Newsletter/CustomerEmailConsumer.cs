using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Customers.Application.Newsletter;
using Romp.Modules.Customers.Domain.Abstractions;
using Romp.Modules.Customers.Infrastructure.Persistence;

namespace Romp.Modules.Customers.Infrastructure.Newsletter;

/// <summary>
/// Turns "send this customer email" (ADR 0022) into the command for its kind. Behind the inbox, so a redelivered message is not sent
/// twice. The command reads the record at send time and throws when the send fails, so the dispatcher retries and the inbox row is not saved
/// until the work is (NFR-FT-08). A kind with no handler yet is an error, not a silent skip.
/// </summary>
public sealed class CustomerEmailConsumer(ISender sender, CustDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<CustomerEmailRequestedEvent>
{
    public async Task HandleAsync(CustomerEmailRequestedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (!await guard.TryBeginAsync(dbContext, "SendCustomerEmail", cancellationToken))
        {
            return;
        }

        switch (domainEvent.Kind)
        {
            case CustomerEmailKind.NewsletterWelcome:
                await sender.Send(new SendNewsletterWelcomeCommand(domainEvent.RelatedId), cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"No handler exists for customer email kind {domainEvent.Kind}.");
        }
    }
}
