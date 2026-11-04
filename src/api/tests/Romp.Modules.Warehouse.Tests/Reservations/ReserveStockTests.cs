using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Warehouse.Application.Reservations;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Reservations;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.Reservations;

[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class ReserveStockTests(WarehousePostgres postgres)
{
    private static int _keySeed;

    private static string NewKey() => $"rsv-{Interlocked.Increment(ref _keySeed)}-{Guid.NewGuid():N}";

    private static async Task<(long RefId, long VariantId, string Sku)> StockedAsync(WarehouseHost host, int units)
    {
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([new StockPost(variant.RefId, Grades.Accepted, bin.Id, units, MovementKinds.Putaway, 77)]));
        return (variant.RefId, variant.VariantId, variant.Sku);
    }

    private static Task<ReservationResult> ReserveAsync(WarehouseHost host, string key, params (long VariantId, int Quantity)[] lines) =>
        host.SendAsync(new ReserveStockCommand(key, [.. lines.Select(l => new ReservationLineRequest(l.VariantId, l.Quantity))]));

    private static async Task<int> AvailableAsync(WarehouseHost host, long variantId) =>
        (await host.InScopeAsync(sp => sp.GetRequiredService<IStockAvailabilityQueries>().GetAsync(variantId, CancellationToken.None)))!.Available;

    [Fact]
    [Trait("Spec", "RSV-1")]
    public async Task EnoughStockForEveryLine_ReservesAll_AndAvailableDropsByTheQuantities()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 10);
        var b = await StockedAsync(host, 4);

        var result = await ReserveAsync(host, NewKey(), (a.VariantId, 3), (b.VariantId, 4));

        Assert.True(result.IsReserved);
        Assert.NotNull(result.ReservationId);
        Assert.Equal(7, await AvailableAsync(host, a.VariantId));
        Assert.Equal(0, await AvailableAsync(host, b.VariantId));
    }

    [Fact]
    [Trait("Spec", "RSV-2")]
    public async Task OneLineShort_ReservesNothing_AndNamesEveryShortVariantWithItsAvailableCount()
    {
        using var host = new WarehouseHost(postgres);
        var plenty = await StockedAsync(host, 10);
        var few = await StockedAsync(host, 2);
        var none = await StockedAsync(host, 1);

        var result = await ReserveAsync(host, NewKey(), (plenty.VariantId, 5), (few.VariantId, 3), (none.VariantId, 2));

        Assert.Equal(ReservationOutcome.Shortage, result.Outcome);
        Assert.Equal([(few.VariantId, 3, 2), (none.VariantId, 2, 1)], result.Shortages.OrderBy(s => s.VariantId).Select(s => (s.VariantId, s.Requested, s.Available)));
        Assert.Equal(10, await AvailableAsync(host, plenty.VariantId));
        await using var db = postgres.NewContext();
        Assert.False(await db.Reservations.AnyAsync(r => r.Lines.Any(l => l.VariantRefId == plenty.RefId)));
    }

    [Fact]
    [Trait("Spec", "RSV-2")]
    public async Task AVariantTheWarehouseDoesNotKnow_IsShortWithNothingAvailable()
    {
        using var host = new WarehouseHost(postgres);

        var result = await ReserveAsync(host, NewKey(), (987_654_321, 1));

        Assert.Equal(ReservationOutcome.Shortage, result.Outcome);
        Assert.Equal(0, Assert.Single(result.Shortages).Available);
    }

    [Fact]
    [Trait("Spec", "RSV-4")]
    public async Task TheSameKeyTwice_IsOneReservation_AndTakesTheStockOnce()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 10);
        var key = NewKey();

        var first = await ReserveAsync(host, key, (a.VariantId, 4));
        var again = await ReserveAsync(host, key, (a.VariantId, 4));

        Assert.Equal(first.ReservationId, again.ReservationId);
        Assert.True(again.IsReserved);
        Assert.Equal(6, await AvailableAsync(host, a.VariantId));
    }

    [Fact]
    [Trait("Spec", "RSV-5")]
    public async Task Releasing_GivesTheUnitsBackOnce_AndARepeatChangesNothing()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 10);
        var key = NewKey();
        await ReserveAsync(host, key, (a.VariantId, 4));

        var first = await host.SendAsync(new ReleaseReservationCommand(key));
        var second = await host.SendAsync(new ReleaseReservationCommand(key));
        var unknown = await host.SendAsync(new ReleaseReservationCommand(NewKey()));

        Assert.True(first);
        Assert.False(second);
        Assert.False(unknown);
        Assert.Equal(10, await AvailableAsync(host, a.VariantId));
        Assert.Equal(ReservationOutcome.Released, (await ReserveAsync(host, key, (a.VariantId, 4))).Outcome);
    }

    [Fact]
    [Trait("Spec", "RSV-7")]
    public async Task EveryReserveAndRelease_RaisesOneStockEventWithTheNewAvailableAndAHigherVersion()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 10);
        var key = NewKey();

        await ReserveAsync(host, key, (a.VariantId, 3));
        await host.SendAsync(new ReleaseReservationCommand(key));

        await using var db = postgres.NewContext();
        var events = db.Set<OutboxMessage>().Where(m => m.EventType == "StockLevelChangedEvent").AsEnumerable()
            .Where(m => m.Payload.Contains(a.Sku, StringComparison.Ordinal)).OrderBy(m => m.Id).Select(m => System.Text.Json.JsonDocument.Parse(m.Payload).RootElement).ToList();
        Assert.Equal(3, events.Count); // the putaway, the reserve, the release
        Assert.Equal([10, 7, 10], events.Select(e => e.GetProperty("Available").GetInt32()));
        Assert.Equal([10, 10, 10], events.Select(e => e.GetProperty("OnHand").GetInt32()));
        var versions = events.Select(e => e.GetProperty("Version").GetInt64()).ToList();
        Assert.Equal(versions.Order(), versions);
        Assert.Equal(3, versions.Distinct().Count());
    }

    [Fact]
    [Trait("Spec", "RSV-3")]
    [Trait("Spec", "NFR-08")]
    public async Task ManyParallelReservationsForOneUnit_ExactlyOneWins()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 1);

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => ReserveAsync(host, NewKey(), (a.VariantId, 1))));

        Assert.Equal(1, results.Count(r => r.IsReserved));
        Assert.Equal(11, results.Count(r => r.Outcome == ReservationOutcome.Shortage));
        Assert.Equal(0, await AvailableAsync(host, a.VariantId));
    }

    [Fact]
    [Trait("Spec", "RSV-3")]
    public async Task TwoOrdersForTheSameVariantsInOppositeOrder_NeverDeadlock_AndNeverOversell()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 5);
        var b = await StockedAsync(host, 5);

        var tasks = Enumerable.Range(0, 10).Select(i => i % 2 == 0
            ? ReserveAsync(host, NewKey(), (a.VariantId, 1), (b.VariantId, 1))
            : ReserveAsync(host, NewKey(), (b.VariantId, 1), (a.VariantId, 1)));
        var results = await Task.WhenAll(tasks);

        Assert.Equal(5, results.Count(r => r.IsReserved));
        Assert.Equal(0, await AvailableAsync(host, a.VariantId));
        Assert.Equal(0, await AvailableAsync(host, b.VariantId));
    }

    [Fact]
    [Trait("Spec", "RSV-2")]
    public async Task TooManyLines_OrABadQuantity_IsRefused()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 5);

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Domain.DomainException>(() => ReserveAsync(host, NewKey(), (a.VariantId, 0)));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Domain.DomainException>(() => host.SendAsync(new ReserveStockCommand(NewKey(), [])));
    }
}
