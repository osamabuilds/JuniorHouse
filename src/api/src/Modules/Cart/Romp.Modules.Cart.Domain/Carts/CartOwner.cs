namespace Romp.Modules.Cart.Domain.Carts;

/// <summary>
/// Whose cart it is: a signed-in customer, or a guest identified only by the SHA-256 of an unguessable cookie value. Exactly one of the
/// two is set. The owner always comes from the session or the guest cookie, never from a request body or route (SEC-05, CRT-9).
/// </summary>
public readonly record struct CartOwner
{
    private CartOwner(long? customerId, string? guestKeyHash)
    {
        CustomerId = customerId;
        GuestKeyHash = guestKeyHash;
    }

    public long? CustomerId { get; }

    public string? GuestKeyHash { get; }

    public bool IsGuest => GuestKeyHash is not null;

    public static CartOwner ForCustomer(long customerId) =>
        customerId > 0 ? new(customerId, null) : throw new ArgumentOutOfRangeException(nameof(customerId));

    public static CartOwner ForGuest(string guestKeyHash) =>
        string.IsNullOrWhiteSpace(guestKeyHash) ? throw new ArgumentException("A guest key hash is required.", nameof(guestKeyHash)) : new(null, guestKeyHash);
}
