namespace Romp.Modules.Customers.Application.Abstractions;

/// <summary>
/// What registration may ask of the newsletter without knowing it (customers/newsletter NLS-4, FR-41). Called only when the person ticked
/// the box: an unticked box never calls it, so it can never re-subscribe an address that unsubscribed.
/// </summary>
public interface INewsletterOptIn
{
    Task SubscribeAtRegistrationAsync(string email, CancellationToken cancellationToken);
}
