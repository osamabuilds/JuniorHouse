namespace Romp.Modules.Customers.Contracts;

/// <summary>One subscribed address. <see cref="Id"/> is the keyset position for the next page.</summary>
public sealed record SubscribedAddress(long Id, string Email);

/// <summary>
/// The only way another module (a future campaign sender) learns who may be mailed (NLS-4, NLS-7, FR-41). It returns subscribed addresses
/// and nothing else: an unsubscribed address is never in it. Paged by keyset so a large list is never loaded at once. The addresses are
/// personal data (NFR-26): the caller sends to them and must not store, log or share them.
/// </summary>
public interface INewsletterAudience
{
    public const int MaxTake = 500;

    /// <summary>Subscribed records with an id above <paramref name="afterId"/>, oldest first. <paramref name="take"/> is clamped to 1..500.</summary>
    Task<IReadOnlyList<SubscribedAddress>> ListSubscribedAsync(long afterId, int take, CancellationToken cancellationToken);
}
