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

    /// <summary>The length of a guest cookie value: 32 random bytes as base64url without padding.</summary>
    public const int GuestCookieLength = 43;

    /// <summary>
    /// Turns a <c>romp-guest</c> cookie value into the stored guest key (its lower-case SHA-256 hex). A value that is not exactly what we
    /// issue (wrong length or characters) is refused, so junk can never name or create a basket.
    /// </summary>
    public static bool TryHashGuestKey(string? raw, out string hash)
    {
        hash = string.Empty;
        if (raw is not { Length: GuestCookieLength } || !raw.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return false;
        }

        hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
        return true;
    }
}
