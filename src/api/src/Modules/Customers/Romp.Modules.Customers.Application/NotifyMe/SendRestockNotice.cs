using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Storefront.Contracts;

namespace Romp.Modules.Customers.Application.NotifyMe;

/// <summary>What the restock email needs to say. The page address is site-relative; Infrastructure joins it to the public base address.</summary>
public sealed record RestockNotice(string ProductName, string ColourName, string SizeName, string PagePath);

/// <summary>Renders and sends the restock email (ADR 0022). A failure throws so the message is retried (NFR-FT-08).</summary>
public interface IRestockMailer
{
    Task SendAsync(string toEmail, RestockNotice notice, CancellationToken cancellationToken);
}

/// <summary>
/// Sends the one restock email for a notified request (FR-11, NTM-5, NTM-9). It reads the request and the variant at send time. A request
/// that is missing, not notified, or already sent gets nothing, so a duplicate message cannot mail twice; a product that is no longer
/// published gets nothing either (there is no page to link to). The variant is not required to be in stock now: if it sold out again the
/// email still goes once (Q-NTM-3). A send failure throws, the transaction rolls back, the request stays Notified with no sent time and the
/// dispatcher retries the message, which never touches stock or the request state (NTM-8).
/// </summary>
public sealed record SendRestockNoticeCommand(long RequestId) : IRequest<bool>, ICustomerCommand;

public sealed class SendRestockNoticeCommandHandler(
    ICustomerDbContext dbContext, IStorefrontCatalogQueries catalog, IRestockMailer mailer, TimeProvider clock)
    : IRequestHandler<SendRestockNoticeCommand, bool>
{
    public async Task<bool> Handle(SendRestockNoticeCommand request, CancellationToken cancellationToken)
    {
        var notify = await dbContext.NotifyRequests.FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);
        if (notify is null || notify.Status != Domain.NotifyMe.NotifyStatus.Notified || notify.SentAt is not null)
        {
            return false;
        }

        var variants = await catalog.GetVariantSummariesAsync([notify.VariantId], cancellationToken);
        if (variants.Count == 0)
        {
            return false;
        }

        var variant = variants[0];
        await mailer.SendAsync(notify.Email, new RestockNotice(variant.ProductName, variant.ColourName, variant.SizeName, variant.Path), cancellationToken);
        notify.MarkSent(clock.GetUtcNow());
        return true;
    }
}
