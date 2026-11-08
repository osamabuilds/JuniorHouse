using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;

namespace Romp.Api.Seeding;

/// <summary>
/// Demo data for "Trending now" (product recommendations REC-4): a few orders for the demo pieces over the last two weeks, one of them
/// cancelled (it must not count). Development only (<c>Demo:SeedSampleData</c>). The orders are written straight into the Orders tables with
/// their events cleared, so the demo does not reserve stock, send a message or touch a basket. Runs once: a second start finds them.
/// </summary>
internal static partial class DemoOrderSeeder
{
    public const string PlacementPrefix = "demo-reco-";

    private const short CodMethod = PaymentMethods.CashOnDelivery;

    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        var orders = services.GetRequiredService<OrdrDbContext>();
        var catalog = services.GetRequiredService<CatalogDbContext>();
        var numbers = services.GetRequiredService<IOrderNumberAllocator>();

        if (await orders.Orders.AnyAsync(o => o.PlacementKey.StartsWith(PlacementPrefix), cancellationToken))
        {
            return;
        }

        var styleIds = await catalog.Styles.Where(s => s.Code.StartsWith(DemoStorefrontSeeder.StyleCodePrefix)).Select(s => s.Id).ToListAsync(cancellationToken);
        var products = await catalog.Products.Where(p => styleIds.Contains(p.StyleId)).OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Name }).Take(12).ToListAsync(cancellationToken);
        if (products.Count < 4)
        {
            return;
        }

        var productIds = products.Select(p => p.Id).ToList();
        var variantByProduct = (await catalog.Variants.Where(v => productIds.Contains(v.ProductId)).OrderBy(v => v.Id)
                .Select(v => new { v.Id, v.ProductId }).ToListAsync(cancellationToken))
            .GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.First().Id);

        var now = DateTimeOffset.UtcNow;
        var sequence = 0;
        var placed = 0;
        for (var i = 0; i < products.Count; i++)
        {
            var product = products[i];
            if (!variantByProduct.TryGetValue(product.Id, out var variantId))
            {
                continue;
            }

            // The first products sell most, so the row has a clear order; the last one is bought once and cancelled.
            var count = i == products.Count - 1 ? 1 : products.Count - i;
            for (var k = 0; k < count; k++)
            {
                var at = now.AddDays(-(1 + ((i + k) % 11))).AddMinutes(-k);
                var order = Place(await numbers.NextAsync(at, cancellationToken), ++sequence, product.Id, variantId, product.Name, 1 + (k % 2), at);
                if (i == products.Count - 1)
                {
                    order.Cancel(CancelReasons.Staff, ActorKinds.System, null, "Demo order, cancelled", wasPicked: false, at.AddMinutes(30));
                    order.ClearDomainEvents();
                }

                orders.Orders.Add(order);
                placed++;
            }
        }

        await orders.SaveChangesAsync(cancellationToken);
        LogOrdersMade(logger, placed);
    }

    private static Order Place(string number, int sequence, long productId, long variantId, string name, int quantity, DateTimeOffset at)
    {
        var order = Order.Place(new PlaceOrderData(
            number,
            $"{PlacementPrefix}{sequence:0000}",
            CustomerId: null,
            new OrderContact("Demo Shopper", null, "03001234567", false),
            new OrderAddress("Demo Shopper", "03001234567", "Demo street 1", null, 1, "Karachi", 1, "Sindh", null, null),
            [new OrderLineData(variantId, productId, null, name, "Demo", "Demo", 1000m, quantity)],
            Discount: 0m,
            Shipping: 250m,
            DiscountCode: null,
            CodMethod,
            CartRef: null,
            at));
        order.ClearDomainEvents();
        return order;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo orders: {Count} orders made for the trending row.")]
    private static partial void LogOrdersMade(ILogger logger, int count);
}
