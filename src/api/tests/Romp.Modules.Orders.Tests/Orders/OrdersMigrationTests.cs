using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Infrastructure.Orders;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class OrdersMigrationTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static int _seq;

    private static Order NewOrder(string number, string key, short method = PaymentMethods.CashOnDelivery) =>
        Order.Place(OrderTests.Data(method, number: number) with { PlacementKey = key });

    private static string NewKey() => $"mig-{Interlocked.Increment(ref _seq)}-{Guid.NewGuid():N}";

    [Fact]
    [Trait("Spec", "ORD-5")]
    public async Task TheLookups_AreSeededWithTheirStableCodes()
    {
        await using var db = postgres.NewContext();

        var statuses = await db.Set<OrderStatusLookup>().OrderBy(s => s.Id).Select(s => s.Code).ToListAsync();
        var methods = await db.Set<PaymentMethodLookup>().OrderBy(s => s.Id).Select(s => s.Code).ToListAsync();
        var reasons = await db.Set<CancelReasonLookup>().OrderBy(s => s.Id).Select(s => s.Code).ToListAsync();

        Assert.Equal(["PNDC", "CNFD", "PYPN", "PAID", "CNCL", "DSPT", "DLVD"], statuses);
        Assert.Equal(["COD", "EASY", "JAZZ", "CARD"], methods);
        Assert.Equal(["STAF", "CODN", "CODR", "CUST", "PYFL", "PYEX"], reasons);
    }

    [Fact]
    [Trait("Spec", "ORD-1")]
    public async Task AnOrder_SavesWithItsLinesAndHistory_AndReadsBackWithTheSnapshots()
    {
        var number = OrderNumbers.Format(2026, 90_001);
        await using (var db = postgres.NewContext())
        {
            var order = NewOrder(number, NewKey());
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            order.MarkPlaced();
            await db.SaveChangesAsync();
        }

        await using var read = postgres.NewContext();
        var saved = await read.Orders.Include(o => o.Lines).Include(o => o.History).SingleAsync(o => o.OrderNumber == number);
        Assert.Equal(2, saved.Lines.Count);
        Assert.Equal(3150m, saved.Total);
        Assert.Equal(("Lahore", "Punjab"), (saved.CityName, saved.ProvinceName));
        Assert.Single(saved.History);
    }

    [Fact]
    [Trait("Spec", "ORD-1")]
    public async Task TheOrderNumber_AndThePlacementKey_AreUnique()
    {
        var number = OrderNumbers.Format(2026, 90_002);
        var key = NewKey();
        await using (var db = postgres.NewContext())
        {
            db.Orders.Add(NewOrder(number, key));
            await db.SaveChangesAsync();
        }

        await using var sameKey = postgres.NewContext();
        sameKey.Orders.Add(NewOrder(OrderNumbers.Format(2026, 90_003), key));
        var byKey = await Assert.ThrowsAsync<DbUpdateException>(() => sameKey.SaveChangesAsync());

        await using var sameNumber = postgres.NewContext();
        sameNumber.Orders.Add(NewOrder(number, NewKey()));
        var byNumber = await Assert.ThrowsAsync<DbUpdateException>(() => sameNumber.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)byKey.InnerException!).SqlState);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)byNumber.InnerException!).SqlState);
    }

    [Fact]
    [Trait("Spec", "SEC-09")]
    public async Task TheDatabase_RefusesAnOrderWhoseAmountsDoNotAddUp()
    {
        await using var db = postgres.NewContext();
        db.Orders.Add(NewOrder(OrderNumbers.Format(2026, 90_004), NewKey()));
        await db.SaveChangesAsync();

        var id = await db.Orders.Select(o => o.Id).OrderByDescending(i => i).FirstAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ORDR\".\"ORDR_MAIN\" SET \"TOT_AMT\" = 1 WHERE \"ID\" = {id}"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    [Trait("Spec", "ORD-5")]
    public async Task TheHistory_IsAppendOnly_TheDatabaseRefusesAnUpdateOrDelete()
    {
        await using var db = postgres.NewContext();
        var order = NewOrder(OrderNumbers.Format(2026, 90_005), NewKey());
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        order.MarkPlaced();
        await db.SaveChangesAsync();

        var update = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"ORDR\".\"ORDR_HIST\" SET \"NOTE\" = 'x'"));
        var delete = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM \"ORDR\".\"ORDR_HIST\""));

        Assert.Contains("append-only", update.MessageText, StringComparison.Ordinal);
        Assert.Contains("append-only", delete.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "ORD-5")]
    public async Task TheNumberAllocator_HandsOutDistinctIncreasingNumbers_UnderConcurrency()
    {
        var numbers = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = postgres.NewContext();
            return await new OrderNumberAllocator(db).NextAsync(new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);
        }));

        Assert.Equal(20, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.True(OrderNumbers.IsValid(n), n));
        Assert.Equal(Enumerable.Range(1, 20).Select(i => OrderNumbers.Format(2031, i)).Order(), numbers.Order());
    }
}
