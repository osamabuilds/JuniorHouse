namespace Romp.Modules.Cart.Contracts;

/// <summary>
/// What holding the basket's discount code for an order came to. <see cref="Applied"/> is false when the basket has no code (and
/// <see cref="Refusal"/> is null) or when the code no longer holds (and <see cref="Refusal"/> says why, in the shopper's words).
/// </summary>
public sealed record DiscountHold(bool Applied, string? Code, decimal Amount, string? Refusal);

/// <summary>
/// How Orders keeps a code's place while an order is being placed (ADR 0025, DSC-10). A hold counts against the code's caps at once, under a
/// lock on the code, so two shoppers racing for the last use cannot both win. The same placement key always gives the same hold.
/// </summary>
public interface IDiscountRedemptions
{
    /// <summary>
    /// Holds the current basket's code for the order identified by <paramref name="placementKey"/>. A guest has no account, so their limit is
    /// kept per <paramref name="guestMobileHash"/> (the hash of the normalised mobile number given at checkout).
    /// </summary>
    Task<DiscountHold> HoldForCurrentCartAsync(string placementKey, string? guestMobileHash, CancellationToken cancellationToken);

    /// <summary>Gives the place back: the order never came. Releasing an unknown or already released key does nothing.</summary>
    Task ReleaseAsync(string placementKey, CancellationToken cancellationToken);
}
