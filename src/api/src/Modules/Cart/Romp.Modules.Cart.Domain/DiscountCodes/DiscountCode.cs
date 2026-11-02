using Romp.BuildingBlocks.Domain;
using Romp.Modules.Cart.Domain.Totals;

namespace Romp.Modules.Cart.Domain.DiscountCodes;

/// <summary>How a code takes money off (Q-DSC-2, approved: percentage and fixed amount only).</summary>
public enum DiscountKind : short
{
    /// <summary>A percentage of the eligible subtotal (1 to 100).</summary>
    Percentage = 1,

    /// <summary>A fixed amount in rupees, never more than the eligible subtotal.</summary>
    Fixed = 2,
}

/// <summary>Where a redemption stands (ADR 0025): held while an order is being placed, confirmed once it exists, released if it never did or was cancelled.</summary>
public enum RedemptionStatus : short
{
    Held = 1,
    Confirmed = 2,
    Released = 3,
}

/// <summary>A scope row: the code applies only to lines in these categories. The category id is a plain reference to Catalog (ADR 0002).</summary>
public sealed class DiscountScopeCategory : Entity<long>, IAuditable
{
    private DiscountScopeCategory()
    {
        InsrBy = string.Empty;
    }

    internal DiscountScopeCategory(short categoryId)
    {
        CategoryId = categoryId;
        InsrBy = string.Empty;
    }

    public long DiscountCodeId { get; private set; }

    public short CategoryId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// A code the shopper can type (FR-25, FR-26, FR-57). The code is stored upper-case and trimmed, so it can be typed any way. Every value is
/// checked here first and again by the database; staff see a specific message for each mistake (DSC-12).
/// </summary>
public sealed class DiscountCode : AggregateRoot<long>, IAuditable
{
    public const int MinCodeLength = 3;
    public const int MaxCodeLength = 32;
    public const decimal MaxFixedValue = 1_000_000m;

    private readonly List<DiscountScopeCategory> _scope = [];

    private DiscountCode()
    {
        Code = string.Empty;
        InsrBy = string.Empty;
    }

    public string Code { get; private set; }

    public DiscountKind Kind { get; private set; }

    public decimal Value { get; private set; }

    /// <summary>The basket must come to at least this much (before the discount) for the code to apply; null means no minimum.</summary>
    public decimal? MinimumSpend { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>The most times the code may be used in all (held and confirmed redemptions count); null means unlimited.</summary>
    public int? UsageCap { get; private set; }

    /// <summary>The most times one customer may use it (Q-DSC-3, default 1); null means unlimited.</summary>
    public int? PerCustomerCap { get; private set; }

    /// <summary>Whether lines with a struck-through price (on sale) count (Q-DSC-4, default no).</summary>
    public bool IncludesSaleItems { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyList<DiscountScopeCategory> Scope => _scope;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static string Normalise(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static DiscountCode Create(
        string code, DiscountKind kind, decimal value, decimal? minimumSpend, DateTimeOffset? expiresAt, int? usageCap, int? perCustomerCap,
        bool includesSaleItems, IEnumerable<short> categories, DateTimeOffset now)
    {
        var created = new DiscountCode { IsActive = true };
        created.Set(code, kind, value, minimumSpend, expiresAt, usageCap, perCustomerCap, includesSaleItems, categories, now, isNew: true);
        return created;
    }

    /// <summary>Changes a code. The code text itself can be changed only while nothing has used it (the service checks that), so history stays readable.</summary>
    public void Update(
        string code, DiscountKind kind, decimal value, decimal? minimumSpend, DateTimeOffset? expiresAt, int? usageCap, int? perCustomerCap,
        bool includesSaleItems, IEnumerable<short> categories, DateTimeOffset now) =>
        Set(code, kind, value, minimumSpend, expiresAt, usageCap, perCustomerCap, includesSaleItems, categories, now, isNew: false);

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    private void Set(
        string code, DiscountKind kind, decimal value, decimal? minimumSpend, DateTimeOffset? expiresAt, int? usageCap, int? perCustomerCap,
        bool includesSaleItems, IEnumerable<short> categories, DateTimeOffset now, bool isNew)
    {
        var normalised = Normalise(code);
        if (normalised.Length is < MinCodeLength or > MaxCodeLength || !normalised.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new DomainException($"A code is {MinCodeLength} to {MaxCodeLength} letters, digits or dashes.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("Choose percentage or fixed amount.");
        }

        if (kind == DiscountKind.Percentage && (value <= 0 || value > 100))
        {
            throw new DomainException("A percentage is more than 0 and at most 100.");
        }

        if (kind == DiscountKind.Fixed && (value <= 0 || value > MaxFixedValue))
        {
            throw new DomainException($"A fixed amount is more than 0 and at most {MaxFixedValue:N0}.");
        }

        if (minimumSpend is < 0)
        {
            throw new DomainException("The minimum spend cannot be negative.");
        }

        if (expiresAt is { } expiry && isNew && expiry <= now)
        {
            throw new DomainException("The expiry must be in the future.");
        }

        if (usageCap is < 1 || perCustomerCap is < 1)
        {
            throw new DomainException("A usage limit is at least 1, or left empty for no limit.");
        }

        var scope = categories.Distinct().ToList();
        if (scope.Any(c => c <= 0))
        {
            throw new DomainException("Choose real categories for the scope.");
        }

        Code = normalised;
        Kind = kind;
        Value = OrderTotals.Round(value);
        MinimumSpend = minimumSpend is { } m ? OrderTotals.Round(m) : null;
        ExpiresAt = expiresAt;
        UsageCap = usageCap;
        PerCustomerCap = perCustomerCap;
        IncludesSaleItems = includesSaleItems;
        _scope.RemoveAll(s => !scope.Contains(s.CategoryId));
        foreach (var category in scope.Where(c => _scope.All(s => s.CategoryId != c)))
        {
            _scope.Add(new DiscountScopeCategory(category));
        }
    }
}
