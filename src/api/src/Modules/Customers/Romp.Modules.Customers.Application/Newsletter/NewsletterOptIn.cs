using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Newsletter;

namespace Romp.Modules.Customers.Application.Newsletter;

/// <summary>The newsletter side of <see cref="INewsletterOptIn"/>: the registration checkbox is an explicit subscribe with the Registration source.</summary>
public sealed class NewsletterOptIn(NewsletterConsent consent) : INewsletterOptIn
{
    public async Task SubscribeAtRegistrationAsync(string email, CancellationToken cancellationToken)
    {
        // The registration email was already checked; an address the newsletter cannot read is skipped, never an error for the sign-up.
        if (NewsletterEmail.Normalize(email) is { } normalised)
        {
            await consent.SubscribeAsync(normalised, ConsentSource.Registration, cancellationToken);
        }
    }
}
