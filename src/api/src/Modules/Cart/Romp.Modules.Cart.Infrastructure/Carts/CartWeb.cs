using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Cart.Application;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Customers.Contracts;

namespace Romp.Modules.Cart.Infrastructure.Carts;

/// <summary>
/// Who owns the basket this request is about (CRT-9, SEC-05): the signed-in customer if there is one, else the guest named by the
/// <c>romp-guest</c> cookie. The owner is read only from the session and that cookie, never from a route, query string or body. The cookie
/// holds 32 random bytes (base64url); only its SHA-256 is stored, so a database leak cannot be turned back into a cookie.
/// </summary>
internal sealed class CartOwnerAccessor(
    IHttpContextAccessor accessor, ICurrentCustomerAccessor customers, IOptions<CartOptions> options, IHostEnvironment environment)
{
    /// <summary>The cookie name. The customer sign-in reads the same cookie to hand a guest basket to the hook (ADR 0023).</summary>
    public const string GuestCookie = "romp-guest";

    /// <summary>The caller's owner identity, or null for a guest who has no cookie yet (so nothing is created by a read).</summary>
    public async Task<CartOwner?> ReadAsync(CancellationToken cancellationToken)
    {
        if (await customers.ResolveAsync(cancellationToken) is { } customerId)
        {
            return CartOwner.ForCustomer(customerId);
        }

        var raw = accessor.HttpContext?.Request.Cookies[GuestCookie];
        return CartOwner.TryHashGuestKey(raw, out var hash) ? CartOwner.ForGuest(hash) : null;
    }

    /// <summary>The caller's owner identity, giving a new guest a cookie the first time they write to a basket.</summary>
    public async Task<CartOwner> ReadOrCreateAsync(CancellationToken cancellationToken)
    {
        if (await ReadAsync(cancellationToken) is { } owner)
        {
            return owner;
        }

        var http = accessor.HttpContext ?? throw new InvalidOperationException("No request.");
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        http.Response.Cookies.Append(GuestCookie, raw, new CookieOptions
        {
            HttpOnly = true,
            // Plain HTTP is allowed only on a developer machine; anywhere else the cookie is Secure.
            Secure = http.Request.IsHttps || !RompAuthorizationExtensions.IsDevelopmentLike(environment),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(options.Value.GuestCartDays),
        });
        CartOwner.TryHashGuestKey(raw, out var created);
        return CartOwner.ForGuest(created);
    }
}


/// <summary>A basket is personal: no shared cache or browser history may keep a response (SEC-25, SEO-05).</summary>
internal sealed class CartNoStoreFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "private, no-store";
        return await next(context);
    }
}

/// <summary>Writes with a cookie and no anti-forgery token need the browser's <c>Origin</c> to be the site's (the rule in <see cref="OriginCheckFilterBase"/>).</summary>
internal sealed class CartOriginCheckFilter(IOptions<SiteOptions> site) : OriginCheckFilterBase(site);

/// <summary>What the browser receives for a basket. Prices, totals and flags are the server's; the line ids are the catalogue's variant ids.</summary>
public sealed record CartLineResponse(
    long VariantId,
    long ProductId,
    string Name,
    string Path,
    string Colour,
    string Size,
    string? Sku,
    decimal? UnitPrice,
    decimal? CompareAtPrice,
    int Quantity,
    decimal LineTotal,
    int Available,
    decimal? PreviousPrice,
    IReadOnlyList<string> Issues,
    string? ImageUrl,
    string? ImageFallbackUrl,
    string? ImageAlt);

/// <summary>
/// The basket and what it costs. <c>Delivery</c> is null until a city is known ("calculated at checkout"); <c>Total</c> then excludes it. Every
/// figure is the server's (SEC-09).
/// </summary>
public sealed record CartResponse(IReadOnlyList<CartLineResponse> Lines, int ItemCount, decimal Subtotal, bool HasProblems, decimal Discount, decimal? Delivery, decimal Total)
{
    public static CartResponse From(PricedCart cart, Romp.Modules.Cart.Domain.Totals.OrderAmounts? amounts = null) =>
        new(
            [.. cart.Lines.Select(l => new CartLineResponse(
                l.VariantId, l.ProductId, l.Name, l.Path, l.Colour, l.Size, l.Sku, l.UnitPrice, l.CompareAtPrice, l.Quantity, l.LineTotal, l.Available,
                l.PreviousPrice, Issues(l.Issues), l.ImageUrl, l.ImageFallbackUrl, l.ImageAlt))],
            cart.ItemCount,
            cart.Subtotal,
            cart.HasProblems,
            amounts?.Discount ?? 0m,
            amounts is { ShippingKnown: true } ? amounts.Shipping : null,
            amounts?.Total ?? cart.Subtotal);

    private static string[] Issues(LineIssues issues) =>
    [
        .. issues.HasFlag(LineIssues.Unavailable) ? ["unavailable"] : Array.Empty<string>(),
        .. issues.HasFlag(LineIssues.LowStock) ? ["low-stock"] : Array.Empty<string>(),
        .. issues.HasFlag(LineIssues.PriceChanged) ? ["price-changed"] : Array.Empty<string>(),
    ];
}
