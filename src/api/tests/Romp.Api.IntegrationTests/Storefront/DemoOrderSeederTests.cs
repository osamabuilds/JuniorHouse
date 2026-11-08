using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Romp.Api.IntegrationTests.Production;
using Romp.Api.Seeding;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;
using Romp.Modules.Storefront.Application.Abstractions;

namespace Romp.Api.IntegrationTests.Storefront;

/// <summary>Product recommendations task X2 (REC-4): the demo shop has orders in the last two weeks, one cancelled, made once, without side effects. Real PostgreSQL.</summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class DemoOrderSeederTests(ProductionApiFixture api)
{
    [Fact]
    [Trait("Spec", "REC-4")]
    public async Task TheDemoOrders_AreMadeOnce_OneIsCancelled_AndTheCancelledOneDoesNotCount()
    {
        for (var pass = 0; pass < 2; pass++)
        {
            await using var scope = api.Services.CreateAsyncScope();
            await DemoStorefrontSeeder.SeedAsync(scope.ServiceProvider, NullLogger.Instance, CancellationToken.None, 3);
            await DemoOrderSeeder.SeedAsync(scope.ServiceProvider, NullLogger.Instance, CancellationToken.None);
        }

        await using var check = api.Services.CreateAsyncScope();
        var orders = check.ServiceProvider.GetRequiredService<OrdrDbContext>();
        var demo = await orders.Orders.AsNoTracking().Include(o => o.Lines).Where(o => o.PlacementKey.StartsWith(DemoOrderSeeder.PlacementPrefix)).ToListAsync();

        Assert.NotEmpty(demo); // made, and not doubled by the second pass
        Assert.Equal(demo.Count, demo.Select(o => o.PlacementKey).Distinct().Count());
        var cancelled = Assert.Single(demo, o => o.StatusId == OrderStatuses.Cancelled);
        Assert.All(demo, o => Assert.True(o.PlacedAt >= DateTimeOffset.UtcNow.AddDays(-14)));

        var sales = await check.ServiceProvider.GetRequiredService<IProductSalesQueries>()
            .UnitsSoldByProductAndDayAsync(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-13)), DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);
        var cancelledProduct = cancelled.Lines.Single().ProductId;
        Assert.DoesNotContain(sales, s => s.ProductId == cancelledProduct);
        Assert.Contains(sales, s => s.Units > 0);

        // No side effects: nothing was queued for another module.
        Assert.Equal(0, await orders.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM \"ORDR\".\"OUTB_MSG\" WHERE \"EVNT_TYP\" IN ('OrderPlacedEvent', 'OrderCancelledEvent') AND \"PYLD\"::text LIKE '%demo-reco-%'").SingleAsync());
    }
}
