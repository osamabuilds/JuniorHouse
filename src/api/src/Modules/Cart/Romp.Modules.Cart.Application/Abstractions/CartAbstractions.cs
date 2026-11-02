using Microsoft.EntityFrameworkCore;
using Romp.Modules.Cart.Domain.Carts;

namespace Romp.Modules.Cart.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Cart so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface ICartCommand;

/// <summary>Cart persistence port (Application owns it, Infrastructure's CartDbContext implements it; ADR 0002).</summary>
public interface ICartDbContext
{
    DbSet<ShoppingCart> Carts { get; }

    DbSet<Domain.Totals.ShippingCityRate> ShippingRates { get; }

    DbSet<Domain.Totals.ShippingSettings> ShippingSettings { get; }

    DbSet<Domain.DiscountCodes.DiscountCode> DiscountCodes { get; }

    DbSet<Domain.DiscountCodes.Redemption> Redemptions { get; }

    /// <summary>Takes a row lock on the code until the transaction ends, so holds on one code are serialised (DSC-10).</summary>
    Task LockDiscountCodeAsync(long discountCodeId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
