namespace Romp.Modules.Customers.Contracts;

/// <summary>Who has just signed in or registered, and the guest session (cart, wishlist) they brought, if any.</summary>
public sealed record SignedInContext(long CustomerId, string? GuestSessionId);

/// <summary>
/// Called after a successful sign-in or registration so other modules can merge what the customer built as a guest (ACT-14, FR-35, FR-17).
/// The cart (S11) and the wishlist register their own implementation. A hook that fails never stops the sign-in. The guest session id is the
/// raw value of the <c>romp-guest</c> cookie; a module that stores only its hash hashes it itself.
/// </summary>
public interface ICustomerSignedInHook
{
    Task OnSignedInAsync(SignedInContext context, CancellationToken cancellationToken);
}

/// <summary>
/// The signed-in customer's id for another module, taken from the session and from nowhere else (SEC-05): it exposes the id and nothing
/// else, and no endpoint or handler may take a customer id from a route, query string or body. Null for a guest.
/// </summary>
public interface ICurrentCustomerAccessor
{
    long? CustomerId { get; }

    /// <summary>
    /// The signed-in customer for a route that is open to guests (so the host has not run the customer scheme for it): authenticates the
    /// customer scheme now, if it has not run, and returns the id, or null for a guest. Never throws for a guest or a bad cookie.
    /// </summary>
    Task<long?> ResolveAsync(CancellationToken cancellationToken) => Task.FromResult(CustomerId);
}
