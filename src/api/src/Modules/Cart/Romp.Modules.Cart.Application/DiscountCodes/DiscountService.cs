using Microsoft.EntityFrameworkCore;
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Domain.DiscountCodes;
using Romp.Modules.Cart.Domain.Totals;

namespace Romp.Modules.Cart.Application.DiscountCodes;

/// <summary>What the shopper is told when a code is refused: specific for a real code, identical for unknown, mistyped and deactivated ones (DSC-9).</summary>
public static class DiscountMessages
{
    public static string For(DiscountResult result, string? appliedCode = null) =>
        result.Rejection switch
        {
            DiscountRejection.Expired => "That code has expired.",
            DiscountRejection.Exhausted => "That code is no longer available.",
            DiscountRejection.AlreadyUsed => "You have already used that code.",
            DiscountRejection.BelowMinimum => $"Spend Rs. {result.MinimumSpend:N0} or more to use that code.",
            DiscountRejection.NoEligibleLines => "That code does not apply to anything in your bag.",
            DiscountRejection.SecondCode => $"Remove {appliedCode ?? "the first code"} first, then apply the new one.",
            _ => "That code is not valid.",
        };
}

/// <summary>
/// Finds a code and meets it with a basket (FR-25, FR-26): the data side of <see cref="DiscountRule"/>. Uses are counted from held and
/// confirmed redemptions, so a held redemption already keeps its place (DSC-10).
/// </summary>
public sealed class DiscountService(ICartDbContext dbContext)
{
    public Task<DiscountCode?> FindAsync(string? code, CancellationToken cancellationToken)
    {
        var normalised = DiscountCode.Normalise(code);
        return normalised.Length is < DiscountCode.MinCodeLength or > DiscountCode.MaxCodeLength
            ? Task.FromResult<DiscountCode?>(null)
            : dbContext.DiscountCodes.Include(c => c.Scope).FirstOrDefaultAsync(c => c.Code == normalised, cancellationToken);
    }

    public Task<DiscountCode?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.DiscountCodes.Include(c => c.Scope).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <summary>The signed-in customer's key for per-customer limits; a guest has none until checkout gives a mobile number (Q-DSC-3).</summary>
    public static string? CustomerKey(CartOwner? owner) => owner?.CustomerId is { } id ? $"C{id}" : null;

    public async Task<DiscountResult> EvaluateAsync(DiscountCode? code, PricedCart cart, DateTimeOffset now, string? customerKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cart);
        if (code is null)
        {
            return DiscountResult.Rejected(DiscountRejection.Unknown);
        }

        var lines = cart.Lines
            .Where(l => !l.Issues.HasFlag(LineIssues.Unavailable) && l.UnitPrice is not null)
            .Select(l => new DiscountLine(l.UnitPrice!.Value, l.Quantity, l.CategoryId, l.IsOnSale))
            .ToList();
        var usedInTotal = await dbContext.Redemptions.CountAsync(r => r.DiscountCodeId == code.Id && r.Status != RedemptionStatus.Released, cancellationToken);
        int? usedByCustomer = customerKey is null
            ? null
            : await dbContext.Redemptions.CountAsync(r => r.DiscountCodeId == code.Id && r.CustomerKey == customerKey && r.Status != RedemptionStatus.Released, cancellationToken);
        return DiscountRule.Evaluate(code, lines, now, usedInTotal, usedByCustomer);
    }
}

/// <summary>
/// Notices a request raised for the shopper while it ran (a code dropped because the basket changed, DSC-13). Scoped, so one request's
/// notices never reach another's; the endpoint adds them to its answer.
/// </summary>
public sealed class CartNotices
{
    private readonly List<string> _items = [];

    public IReadOnlyList<string> Items => _items;

    public void Add(string notice) => _items.Add(notice);
}

/// <summary>The money off for a basket under its applied code, or a notice saying the code no longer holds. Never throws for a rejected code.</summary>
public sealed record AppliedDiscount(string? Code, decimal Amount, string? Notice);

public static class DiscountOutcome
{
    public static async Task<AppliedDiscount> ResolveAsync(
        DiscountService discounts, ShoppingCart? cart, PricedCart priced, DateTimeOffset now, string? customerKey, CancellationToken cancellationToken)
    {
        if (cart?.AppliedCodeId is not { } codeId || priced.IsEmpty)
        {
            return new AppliedDiscount(null, 0m, null);
        }

        var code = await discounts.FindByIdAsync(codeId, cancellationToken);
        var result = await discounts.EvaluateAsync(code, priced, now, customerKey, cancellationToken);
        return result.IsAccepted
            ? new AppliedDiscount(code!.Code, OrderTotals.Round(result.Amount), null)
            : new AppliedDiscount(null, 0m, $"{code?.Code ?? "The code"} was taken off: {DiscountMessages.For(result).TrimEnd('.').ToLowerInvariant()}.");
    }
}
