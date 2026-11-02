using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Cart.Domain.Carts;

/// <summary>What a merge had to adjust, so the shopper is told rather than surprised (CRT-3).</summary>
public sealed record MergeNotice(long VariantId, int Requested, int Kept);

/// <summary>
/// The shopper's basket (CRT-1 to CRT-11). One line per variant; quantity 1 to the per-line maximum. It holds variant ids and quantities
/// only; prices, names and availability are looked up when the cart is read (SEC-09). A cart belongs to exactly one owner and an
/// owner has at most one cart (unique indexes back both rules).
/// </summary>
public sealed class ShoppingCart : AggregateRoot<long>, IAuditable
{
    public const int DefaultMaxPerLine = 10;
    public const int DefaultMaxLines = 50;

    private readonly List<CartLine> _lines = [];

    private ShoppingCart()
    {
        InsrBy = string.Empty;
    }

    public long? CustomerId { get; private set; }

    public string? GuestKeyHash { get; private set; }

    public long? AppliedCodeId { get; private set; }

    public DateTimeOffset LastTouchedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public IReadOnlyList<CartLine> Lines => _lines;

    public bool IsEmpty => _lines.Count == 0;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>A new, empty cart. A guest cart expires after <paramref name="guestLifetime"/> of no use (CRT-10); an account cart does not.</summary>
    public static ShoppingCart Create(CartOwner owner, DateTimeOffset now, TimeSpan guestLifetime) =>
        new()
        {
            CustomerId = owner.CustomerId,
            GuestKeyHash = owner.GuestKeyHash,
            LastTouchedAt = now,
            ExpiresAt = owner.IsGuest ? now + guestLifetime : null,
        };

    /// <summary>Whether this guest cart is past its lifetime (an account cart never is).</summary>
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expires && now >= expires;

    /// <summary>
    /// Adds <paramref name="quantity"/> to the variant's line (creating it). The total for the line may not pass <paramref name="maxPerLine"/>
    /// (CRT-11); the stock check belongs to the application, because stock lives in Warehouse (CRT-4).
    /// </summary>
    public void AddLine(long variantId, int quantity, int maxPerLine, int maxLines, DateTimeOffset now, TimeSpan guestLifetime)
    {
        RequireVariant(variantId);
        RequireQuantity(quantity, maxPerLine);

        var line = _lines.FirstOrDefault(l => l.VariantId == variantId);
        if (line is null)
        {
            if (_lines.Count >= maxLines)
            {
                throw new DomainException($"A basket holds at most {maxLines} different items.");
            }

            _lines.Add(new CartLine(variantId, quantity));
        }
        else
        {
            var total = line.Quantity + quantity;
            RequireQuantity(total, maxPerLine);
            line.Quantity = total;
        }

        Touch(now, guestLifetime);
    }

    /// <summary>Sets the quantity of an existing line (CRT-1). A quantity below 1 is refused: removing is a separate, explicit act (CRT-11).</summary>
    public void SetQuantity(long variantId, int quantity, int maxPerLine, DateTimeOffset now, TimeSpan guestLifetime)
    {
        RequireQuantity(quantity, maxPerLine);
        var line = _lines.FirstOrDefault(l => l.VariantId == variantId)
            ?? throw new DomainException("That item is not in the basket.");
        line.Quantity = quantity;
        Touch(now, guestLifetime);
    }

    /// <summary>Removes a line; removing the last one leaves an empty basket (CRT-2). Removing a variant that is not there does nothing.</summary>
    public void RemoveLine(long variantId, DateTimeOffset now, TimeSpan guestLifetime)
    {
        var line = _lines.FirstOrDefault(l => l.VariantId == variantId);
        if (line is null)
        {
            return;
        }

        _lines.Remove(line);
        if (_lines.Count == 0)
        {
            AppliedCodeId = null; // a code with nothing to discount makes no sense (DSC-13)
        }

        Touch(now, guestLifetime);
    }

    /// <summary>
    /// Merges a guest's lines into this (account) cart on sign-in: a variant in both is combined into one line, capped at the available
    /// stock (when known) and at <paramref name="maxPerLine"/>, and nothing is lost or duplicated (CRT-3, FR-35). A quantity the account
    /// cart already held is never reduced by the merge. Returns a notice for each line that had to be reduced.
    /// </summary>
    public IReadOnlyList<MergeNotice> MergeFrom(
        ShoppingCart guest, IReadOnlyDictionary<long, int> available, int maxPerLine, int maxLines, DateTimeOffset now, TimeSpan guestLifetime)
    {
        ArgumentNullException.ThrowIfNull(guest);
        ArgumentNullException.ThrowIfNull(available);

        var notices = new List<MergeNotice>();
        foreach (var incoming in guest.Lines)
        {
            var existing = _lines.FirstOrDefault(l => l.VariantId == incoming.VariantId);
            var held = existing?.Quantity ?? 0;
            var wanted = held + incoming.Quantity;
            var cap = Math.Min(maxPerLine, available.TryGetValue(incoming.VariantId, out var stock) ? stock : maxPerLine);
            var kept = Math.Min(wanted, Math.Max(cap, held));

            if (kept < wanted)
            {
                notices.Add(new MergeNotice(incoming.VariantId, wanted, kept));
            }

            if (kept <= 0)
            {
                continue;
            }

            if (existing is not null)
            {
                existing.Quantity = kept;
            }
            else if (_lines.Count < maxLines)
            {
                _lines.Add(new CartLine(incoming.VariantId, kept));
            }
            else
            {
                notices.Add(new MergeNotice(incoming.VariantId, incoming.Quantity, 0));
            }
        }

        Touch(now, guestLifetime);
        return notices;
    }

    public void ApplyCode(long codeId, DateTimeOffset now, TimeSpan guestLifetime)
    {
        if (IsEmpty)
        {
            throw new DomainException("Add something to the basket before applying a code.");
        }

        AppliedCodeId = codeId;
        Touch(now, guestLifetime);
    }

    public void RemoveCode(DateTimeOffset now, TimeSpan guestLifetime)
    {
        AppliedCodeId = null;
        Touch(now, guestLifetime);
    }

    /// <summary>Empties the basket once its order has been placed (ADR 0025 step 7).</summary>
    public void Clear(DateTimeOffset now, TimeSpan guestLifetime)
    {
        _lines.Clear();
        AppliedCodeId = null;
        Touch(now, guestLifetime);
    }

    private void Touch(DateTimeOffset now, TimeSpan guestLifetime)
    {
        LastTouchedAt = now;
        if (GuestKeyHash is not null)
        {
            ExpiresAt = now + guestLifetime;
        }
    }

    private static void RequireVariant(long variantId)
    {
        if (variantId <= 0)
        {
            throw new DomainException("Choose a size and colour first.");
        }
    }

    private static void RequireQuantity(int quantity, int maxPerLine)
    {
        if (quantity < 1)
        {
            throw new DomainException("The quantity must be at least 1. Use remove to take an item out.");
        }

        if (quantity > maxPerLine)
        {
            throw new DomainException($"You can buy at most {maxPerLine} of one item in a single order.");
        }
    }
}
