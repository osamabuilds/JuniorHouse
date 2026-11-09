using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Fulfilment.Application.PackStation;
using Romp.Modules.Fulfilment.Application.PickLists;
using Romp.Modules.Fulfilment.Domain.Allocations;

namespace Romp.Modules.Fulfilment.Tests.Support;

/// <summary>Short helpers so a test reads as the steps of the flow it checks.</summary>
internal static class Flow
{
    public static Task<ClaimResult> ClaimAsync(FulfilmentHost host, long id, string staff = FulfilmentHost.Picker) => host.SendAsync(new ClaimPickCommand(id, staff));

    public static async Task<ScanOutcomeDto> ScanAsync(FulfilmentHost host, long id, string code, string staff = FulfilmentHost.Picker) =>
        (await host.SendAsync(new ScanPickCommand(id, staff, code)))!;

    /// <summary>Claims the order and scans every bin and unit on its pick list.</summary>
    public static async Task PickEverythingAsync(FulfilmentHost host, FulfilmentPostgres postgres, long id, string staff = FulfilmentHost.Picker)
    {
        await ClaimAsync(host, id, staff);
        var list = (await host.SendAsync(new GetPickListQuery(id)))!;
        await using var db = postgres.NewContext();
        var gtins = await db.Lines.Where(l => l.OrderId == id).ToDictionaryAsync(l => l.VariantId, l => l.Gtin!);
        foreach (var row in list.Rows)
        {
            await ScanAsync(host, id, row.BinCode, staff);
            for (var i = 0; i < row.Quantity; i++)
            {
                var scan = await ScanAsync(host, id, gtins[row.VariantId], staff);
                Assert.True(scan.Accepted, scan.Message);
            }
        }
    }

    public static async Task PickAndCompleteAsync(FulfilmentHost host, FulfilmentPostgres postgres, long id, string staff = FulfilmentHost.Picker)
    {
        await PickEverythingAsync(host, postgres, id, staff);
        await host.SendAsync(new CompletePickCommand(id, staff));
    }

    /// <summary>Opens the packer, scans every unit, and returns the lines' GTINs by variant.</summary>
    public static async Task PackScanAllAsync(FulfilmentHost host, FulfilmentPostgres postgres, long id, string staff = FulfilmentHost.Packer)
    {
        await host.SendAsync(new OpenPackCommand(id, staff));
        await using var db = postgres.NewContext();
        foreach (var line in await db.Lines.Where(l => l.OrderId == id).ToListAsync())
        {
            for (var i = 0; i < line.Quantity; i++)
            {
                var scan = (await host.SendAsync(new ScanPackCommand(id, staff, line.Gtin!)))!;
                Assert.True(scan.Accepted, scan.Message);
            }
        }
    }

    public static async Task<FulfilmentOrder> LoadAsync(FulfilmentPostgres postgres, long id)
    {
        await using var db = postgres.NewContext();
        return await db.Orders.AsNoTracking().AsSplitQuery().Include(o => o.Lines).ThenInclude(l => l.Allocations).Include(o => o.Exceptions).Include(o => o.Pack)
            .SingleAsync(o => o.Id == id);
    }

    public static async Task<List<string>> EventsAsync(FulfilmentPostgres postgres, string eventType, string orderNumber)
    {
        await using var db = postgres.NewContext();
        var payloads = await db.Set<OutboxMessage>().Where(m => m.EventType == eventType).Select(m => m.Payload).ToListAsync();
        return [.. payloads.Where(p => p.Contains(orderNumber, StringComparison.Ordinal))];
    }
}
