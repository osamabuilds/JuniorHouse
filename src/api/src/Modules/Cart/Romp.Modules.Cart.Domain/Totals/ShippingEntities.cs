using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Cart.Domain.Totals;

/// <summary>The most one fee may be (a sanity limit, in rupees), so a typo cannot charge a million.</summary>
public static class ShippingLimits
{
    public const decimal MaxFee = 100_000m;

    public static void RequireFee(decimal fee, string field)
    {
        if (fee < 0 || fee > MaxFee)
        {
            throw new DomainException($"{field} must be between 0 and {MaxFee:N0}.");
        }
    }
}

/// <summary>
/// The delivery fee for one city (Q-TOT-1, owner 2026-11-02: staff set it in the admin). The city id is a plain reference into the Reference
/// lookups (no key crosses a schema, ADR 0002). One rate per city; a city with none uses the default fee.
/// </summary>
public sealed class ShippingCityRate : Entity<long>, IAuditable
{
    private ShippingCityRate()
    {
        InsrBy = string.Empty;
    }

    public ShippingCityRate(short cityId, decimal fee)
    {
        if (cityId <= 0)
        {
            throw new DomainException("Choose a city.");
        }

        ShippingLimits.RequireFee(fee, "The delivery fee");
        CityId = cityId;
        Fee = OrderTotals.Round(fee);
        InsrBy = string.Empty;
    }

    public short CityId { get; private set; }

    public decimal Fee { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public void SetFee(decimal fee)
    {
        ShippingLimits.RequireFee(fee, "The delivery fee");
        Fee = OrderTotals.Round(fee);
    }
}

/// <summary>
/// The shop-wide delivery settings, one row: the fee for a city with no rate of its own, an optional free-delivery threshold and an optional
/// extra fee for Cash on Delivery (Q-TOT-1). The migration seeds a zero default; a health warning flags a zero default in production until
/// the business sets it.
/// </summary>
public sealed class ShippingSettings : Entity<long>, IAuditable
{
    /// <summary>The one row's id.</summary>
    public const long TheOne = 1;

    private ShippingSettings()
    {
        InsrBy = string.Empty;
    }

    public ShippingSettings(decimal defaultFee, decimal? freeThreshold, decimal? codFee)
    {
        Id = TheOne;
        InsrBy = string.Empty;
        Set(defaultFee, freeThreshold, codFee);
    }

    public decimal DefaultFee { get; private set; }

    public decimal? FreeThreshold { get; private set; }

    public decimal? CodFee { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public void Set(decimal defaultFee, decimal? freeThreshold, decimal? codFee)
    {
        ShippingLimits.RequireFee(defaultFee, "The default delivery fee");
        if (freeThreshold is { } threshold && threshold <= 0)
        {
            throw new DomainException("The free-delivery amount must be more than zero, or left empty for no free delivery.");
        }

        if (codFee is { } cod)
        {
            ShippingLimits.RequireFee(cod, "The Cash on Delivery fee");
        }

        DefaultFee = OrderTotals.Round(defaultFee);
        FreeThreshold = freeThreshold is { } t ? OrderTotals.Round(t) : null;
        CodFee = codFee is { } c ? OrderTotals.Round(c) : null;
    }
}
