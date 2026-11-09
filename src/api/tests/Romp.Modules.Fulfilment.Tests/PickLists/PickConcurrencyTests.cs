using Microsoft.EntityFrameworkCore;
using Romp.Modules.Fulfilment.Application.Allocations;
using Romp.Modules.Fulfilment.Application.PickLists;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Tests.Support;

namespace Romp.Modules.Fulfilment.Tests.PickLists;

/// <summary>S14 T2: several people or several messages at once never double-claim, double-count or double-complete (PCK-8, PCK-9, ALC-5, NFR-08).</summary>
[Collection(FulfilmentPostgresFixtureDefinition.Name)]
public sealed class PickConcurrencyTests(FulfilmentPostgres postgres)
{
    private static async Task<(bool Ok, Exception? Error)> TryAsync(Func<Task> action)
    {
        try
        {
            await action();
            return (true, null);
        }
        catch (Exception ex) when (ex is DbUpdateException or Romp.BuildingBlocks.Domain.DomainException)
        {
            return (false, ex);
        }
    }

    [Fact]
    [Trait("Spec", "PCK-9")]
    public async Task ManyPickersTakingTheSameOrderAtOnce_LeaveExactlyOneHolder()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (2, "A-01-1", 10));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        var pickers = Enumerable.Range(1, 8).Select(n => $"Picker {n}").ToList();

        var results = await Task.WhenAll(pickers.Select(async p =>
        {
            ClaimResult? claim = null;
            var attempt = await TryAsync(async () => claim = await Flow.ClaimAsync(host, id, p));
            return (Picker: p, Won: attempt.Ok && claim!.Claimed);
        }));

        var winners = results.Where(r => r.Won).ToList();
        Assert.Single(winners);
        var order = await Flow.LoadAsync(postgres, id);
        Assert.Equal(winners[0].Picker, order.PickClaimedBy);
        Assert.Equal(FulfilmentStatuses.Picking, order.StatusId);
    }

    [Fact]
    [Trait("Spec", "PCK-8")]
    public async Task TheSameCompletionRepeatedAtOnce_DecrementsOnce_AndRaisesOneEvent()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (2, "A-01-1", 10));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        await Flow.PickEverythingAsync(host, postgres, id);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            TryAsync(() => host.SendAsync(new CompletePickCommand(id, FulfilmentHost.Picker)))));

        Assert.Contains(results, r => r.Ok);
        Assert.Single(host.Allocator.Confirmed);
        Assert.Single(await Flow.EventsAsync(postgres, "PickCompletedEvent", placed.OrderNumber));
        Assert.Equal(FulfilmentStatuses.Picked, (await Flow.LoadAsync(postgres, id)).StatusId);
    }

    [Fact]
    [Trait("Spec", "PCK-5")]
    public async Task ManyScansOfTheLastUnitAtOnce_CountItOnce()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 10));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        await Flow.ClaimAsync(host, id);
        await Flow.ScanAsync(host, id, "A-01-1");

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            var accepted = false;
            var attempt = await TryAsync(async () => accepted = (await Flow.ScanAsync(host, id, placed.Gtins[0])).Accepted);
            return attempt.Ok && accepted;
        }));

        Assert.Equal(1, results.Count(r => r));
        var line = (await Flow.LoadAsync(postgres, id)).Lines.Single();
        Assert.Equal(1, line.PickedQuantity);
        Assert.Equal(1, line.Allocations.Single().PickedQuantity);
    }

    [Fact]
    [Trait("Spec", "ALC-5")]
    public async Task TheSameConfirmationDeliveredManyTimesAtOnce_LeavesOneFulfilmentOrder()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 10));

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            TryAsync(() => host.SendAsync(new CreateFulfilmentOrderCommand(placed.OrderId)))));

        Assert.Contains(results, r => r.Ok);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Orders.CountAsync(o => o.OrderId == placed.OrderId));
    }
}
