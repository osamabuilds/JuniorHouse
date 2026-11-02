using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Reservations;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Reservations;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.Reservations;

[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class ReservationLifecycleTests(WarehousePostgres postgres)
{
    private static string NewKey() => $"life-{Guid.NewGuid():N}";

    private static async Task<(long RefId, long VariantId, string Sku, long BinId)> StockedAsync(WarehouseHost host, int units)
    {
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([new StockPost(variant.RefId, Grades.Accepted, bin.Id, units, MovementKinds.Putaway, 77)]));
        return (variant.RefId, variant.VariantId, variant.Sku, bin.Id);
    }

    private static Task<ReservationResult> ReserveAsync(WarehouseHost host, string key, long variantId, int quantity) =>
        host.SendAsync(new ReserveStockCommand(key, [new ReservationLineRequest(variantId, quantity)]));

    private static async Task<int> AvailableAsync(WarehouseHost host, long variantId) =>
        (await host.InScopeAsync(sp => sp.GetRequiredService<IStockAvailabilityQueries>().GetAsync(variantId, CancellationToken.None)))!.Available;

    private async Task ConfirmAsync(string key)
    {
        await using var db = postgres.NewContext();
        var reservation = await db.Reservations.SingleAsync(r => r.PlacementKey == key);
        reservation.Confirm("RMP-2026-00001-1");
        await db.SaveChangesAsync();
    }

    [Fact]
    [Trait("Spec", "RSV-6")]
    public async Task TheSweeper_ReleasesHoldsPastTheirExpiry_AndLeavesFreshAndConfirmedOnesAlone()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 10);
        var expired = NewKey();
        var confirmed = NewKey();
        await ReserveAsync(host, expired, a.VariantId, 2);
        await ReserveAsync(host, confirmed, a.VariantId, 3);
        await ConfirmAsync(confirmed);
        host.Time.Now = WarehouseHost.Start.AddMinutes(10);
        var fresh = NewKey();
        await ReserveAsync(host, fresh, a.VariantId, 1);
        Assert.Equal(4, await AvailableAsync(host, a.VariantId));

        host.Time.Now = WarehouseHost.Start.AddMinutes(31);
        var released = await host.SendAsync(new SweepHeldReservationsCommand());

        Assert.True(released >= 1);
        await using var db = postgres.NewContext();
        var states = await db.Reservations.Where(r => r.PlacementKey == expired || r.PlacementKey == confirmed || r.PlacementKey == fresh).ToDictionaryAsync(r => r.PlacementKey, r => r.StatusId);
        Assert.Equal(ReservationStatuses.Released, states[expired]);
        Assert.Equal(ReservationStatuses.Confirmed, states[confirmed]);
        Assert.Equal(ReservationStatuses.Held, states[fresh]);
        Assert.Equal(6, await AvailableAsync(host, a.VariantId));
    }

    [Fact]
    [Trait("Spec", "RSV-9")]
    public async Task Converting_NeedsAConfirmedReservation_DropsItsReservedUnits_AndRepeatsChangeNothing()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 10);
        var key = NewKey();
        await ReserveAsync(host, key, a.VariantId, 4);

        Assert.False(await host.SendAsync(new ConvertReservationCommand(key)));
        await ConfirmAsync(key);
        Assert.True(await host.SendAsync(new ConvertReservationCommand(key)));
        Assert.False(await host.SendAsync(new ConvertReservationCommand(key)));

        // The pick (S14) will decrement the ledger by the same units in the same transaction; here only the reserved side moves.
        Assert.Equal(10, await AvailableAsync(host, a.VariantId));
        await using var db = postgres.NewContext();
        Assert.Equal(ReservationStatuses.Converted, (await db.Reservations.SingleAsync(r => r.PlacementKey == key)).StatusId);
    }

    [Fact]
    [Trait("Spec", "RSV-8")]
    public async Task AChangeThatLeavesLessThanIsReserved_IsRefused_AndOneThatLeavesEnoughIsApplied()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 10);
        await ReserveAsync(host, NewKey(), a.VariantId, 8);

        var tooMuch = await Assert.ThrowsAsync<DomainException>(() =>
            host.SendAsync(new TestPostCommand([new StockPost(a.RefId, Grades.Accepted, a.BinId, -3, MovementKinds.Adjustment, 78)])));
        await host.SendAsync(new TestPostCommand([new StockPost(a.RefId, Grades.Accepted, a.BinId, -2, MovementKinds.Adjustment, 79)]));

        Assert.Equal(StockMessages.BelowReserved, tooMuch.Message);
        Assert.Equal(0, await AvailableAsync(host, a.VariantId));
        await using var db = postgres.NewContext();
        Assert.Equal(8, await db.Balances.Where(b => b.VariantRefId == a.RefId).SumAsync(b => b.Quantity));
    }

    [Fact]
    [Trait("Spec", "RSV-8")]
    public async Task AdditionsAreNeverRefused_EvenWhenEverythingIsReserved()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 3);
        await ReserveAsync(host, NewKey(), a.VariantId, 3);

        await host.SendAsync(new TestPostCommand([new StockPost(a.RefId, Grades.Accepted, a.BinId, 5, MovementKinds.Adjustment, 80)]));

        Assert.Equal(5, await AvailableAsync(host, a.VariantId));
    }

    [Fact]
    [Trait("Spec", "RSV-7")]
    public async Task TheStaffList_ShowsTheReservationsHoldingAVariant_WithTheirOrderNumbersOnly()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 10);
        var key = NewKey();
        await ReserveAsync(host, key, a.VariantId, 2);
        await ConfirmAsync(key);

        var page = await host.SendAsync(new ListVariantReservationsQuery(a.RefId));

        var row = Assert.Single(page.Items);
        Assert.Equal((key, "CONFIRMED", 2, "RMP-2026-00001-1"), (row.PlacementKey, row.StatusCode, row.Quantity, row.OrderNumber));
        var stock = (await host.SendAsync(new SearchStockQuery(a.Sku))).Items.Single();
        Assert.Equal(2, stock.Reserved);
        Assert.Equal(8, stock.Available);
    }
}
