using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Reservations;
using Romp.Modules.Warehouse.Contracts;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Reservations;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.Reservations;

[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class ConfirmReservationTests(WarehousePostgres postgres)
{
    private static string NewKey() => $"conf-{Guid.NewGuid():N}";

    private static async Task<(long RefId, long VariantId, long BinId)> StockedAsync(WarehouseHost host, int units)
    {
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([new StockPost(variant.RefId, Grades.Accepted, bin.Id, units, MovementKinds.Putaway, 77)]));
        return (variant.RefId, variant.VariantId, bin.Id);
    }

    private static Task<ReservationResult> ReserveAsync(WarehouseHost host, string key, long variantId, int quantity) =>
        host.SendAsync(new ReserveStockCommand(key, [new ReservationLineRequest(variantId, quantity)]));

    private static async Task<int> AvailableAsync(WarehouseHost host, long variantId) =>
        (await host.InScopeAsync(sp => sp.GetRequiredService<IStockAvailabilityQueries>().GetAsync(variantId, CancellationToken.None)))!.Available;

    [Fact]
    [Trait("Spec", "RSV-4")]
    public async Task Confirming_FirmsTheHold_AndARepeatChangesNothing()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 10);
        var key = NewKey();
        await ReserveAsync(host, key, a.VariantId, 3);

        var first = await host.SendAsync(new ConfirmReservationCommand(key, "RMP-2026-00001-4"));
        var again = await host.SendAsync(new ConfirmReservationCommand(key, "RMP-2026-00001-4"));

        Assert.Equal((ConfirmOutcome.Confirmed, ConfirmOutcome.Confirmed), (first, again));
        await using var db = postgres.NewContext();
        var saved = await db.Reservations.SingleAsync(r => r.PlacementKey == key);
        Assert.Equal((ReservationStatuses.Confirmed, "RMP-2026-00001-4"), (saved.StatusId, saved.OrderNumber));
        Assert.Equal(7, await AvailableAsync(host, a.VariantId));
    }

    [Fact]
    [Trait("Spec", "RSV-6")]
    public async Task ALateOrderEvent_ReservesTheSameStockAgain_WhenTheSweeperGotThereFirst()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 10);
        var key = NewKey();
        await ReserveAsync(host, key, a.VariantId, 4);
        host.Time.Now = WarehouseHost.Start.AddMinutes(31);
        await host.SendAsync(new SweepHeldReservationsCommand());
        Assert.Equal(10, await AvailableAsync(host, a.VariantId));

        var outcome = await host.SendAsync(new ConfirmReservationCommand(key, "RMP-2026-00002-2"));

        Assert.Equal(ConfirmOutcome.Reinstated, outcome);
        Assert.Equal(6, await AvailableAsync(host, a.VariantId));
        await using var db = postgres.NewContext();
        Assert.Equal(ReservationStatuses.Confirmed, (await db.Reservations.SingleAsync(r => r.PlacementKey == key)).StatusId);
    }

    [Fact]
    [Trait("Spec", "RSV-6")]
    public async Task ALateOrderEvent_WhenTheStockIsGone_FailsLoudly_ForStaffToResolve()
    {
        using var host = new WarehouseHost(postgres);
        var a = await StockedAsync(host, 2);
        var key = NewKey();
        await ReserveAsync(host, key, a.VariantId, 2);
        host.Time.Now = WarehouseHost.Start.AddMinutes(31);
        await host.SendAsync(new SweepHeldReservationsCommand());
        await ReserveAsync(host, NewKey(), a.VariantId, 2);

        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ConfirmReservationCommand(key, "RMP-2026-00003-0")));

        Assert.Contains("Staff must resolve it", ex.Message, StringComparison.Ordinal);
        await using var db = postgres.NewContext();
        Assert.Equal(ReservationStatuses.Released, (await db.Reservations.SingleAsync(r => r.PlacementKey == key)).StatusId);
    }

    [Fact]
    [Trait("Spec", "RSV-4")]
    public async Task ConfirmingAnUnknownKey_Fails_SoTheMessageIsNotSilentlyLost()
    {
        using var host = new WarehouseHost(postgres);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ConfirmReservationCommand(NewKey(), "RMP-2026-00004-8")));
    }
}
