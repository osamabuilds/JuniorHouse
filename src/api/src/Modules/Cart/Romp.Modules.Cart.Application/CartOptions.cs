using Romp.Modules.Cart.Domain.Carts;

namespace Romp.Modules.Cart.Application;

/// <summary>The <c>Cart</c> configuration section (module plan, "Policies, configuration and the web edge"). Validated at start-up.</summary>
public sealed class CartOptions
{
    public const string SectionName = "Cart";

    /// <summary>The most of one variant in one order (Q-CRT-3; the owner has not answered, so this is the plan's assumption).</summary>
    public int MaxPerLine { get; set; } = ShoppingCart.DefaultMaxPerLine;

    /// <summary>The most different variants in one basket.</summary>
    public int MaxLines { get; set; } = ShoppingCart.DefaultMaxLines;

    /// <summary>How long a guest basket lives after its last use (Q-CRT-2, approved: 30 days). An account basket does not expire.</summary>
    public int GuestCartDays { get; set; } = 30;

    /// <summary>How long a held discount redemption waits for its order before it is released (ADR 0025).</summary>
    public int HoldMinutes { get; set; } = 30;

    public TimeSpan GuestLifetime => TimeSpan.FromDays(GuestCartDays);

    public static bool IsValid(CartOptions options) =>
        options.MaxPerLine is >= 1 and <= 100
        && options.MaxLines is >= 1 and <= 200
        && options.GuestCartDays is >= 1 and <= 365
        && options.HoldMinutes is >= 5 and <= 1440;
}
