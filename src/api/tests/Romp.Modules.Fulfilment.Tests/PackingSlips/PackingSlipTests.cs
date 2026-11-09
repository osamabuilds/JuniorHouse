using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Fulfilment.Application.PackStation;
using Romp.Modules.Fulfilment.Application.PackingSlips;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Tests.Support;

namespace Romp.Modules.Fulfilment.Tests.PackingSlips;

/// <summary>S14 F11: the slip inside the parcel, built from Orders at print time (SLP-1 to SLP-8).</summary>
[Collection(FulfilmentPostgresFixtureDefinition.Name)]
public sealed class PackingSlipTests(FulfilmentPostgres postgres)
{
    private static async Task<(FulfilmentHost Host, long Id, (long OrderId, string OrderNumber, long[] VariantIds, string[] Gtins) Placed)> PackingAsync(
        FulfilmentPostgres postgres, bool cod = true)
    {
        var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(cod, (2, "A-01-1", 10), (1, "B-02-2", 10));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);
        await Flow.PickAndCompleteAsync(host, postgres, id);
        await host.SendAsync(new OpenPackCommand(id, FulfilmentHost.Packer));
        return (host, id, placed);
    }

    [Fact]
    [Trait("Spec", "SLP-1")]
    public async Task TheSlip_ListsTheOrderNumberEachLineWithSkuSizeColourQuantityAndPrice_AndTheTotals()
    {
        var (host, id, placed) = await PackingAsync(postgres);
        using var _ = host;

        var slip = (await host.SendAsync(new GetPackingSlipQuery(id)))!;

        Assert.Equal(placed.OrderNumber, slip.OrderNumber);
        Assert.Equal(2, slip.Lines.Count);
        var line = slip.Lines[0];
        Assert.Equal(($"SKU-{placed.VariantIds[0]}", "2-3Y", "Red", 2, 1500m, 3000m), (line.Sku, line.Size, line.Colour, line.Quantity, line.UnitPrice, line.LineTotal));
        Assert.Equal((4500m, 200m, 4700m), (slip.Subtotal, slip.Shipping, slip.Total));
    }

    [Fact]
    [Trait("Spec", "SLP-2")]
    public async Task ACashOnDeliverySlip_PrintsTheAmountToCollect_AndIsNotMarkedPaid()
    {
        var (host, id, _) = await PackingAsync(postgres, cod: true);
        using var _ = host;

        var slip = (await host.SendAsync(new GetPackingSlipQuery(id)))!;

        Assert.True(slip.IsCashOnDelivery);
        Assert.Equal(4700m, slip.CashToCollect);
        Assert.False(slip.IsPaid);
    }

    [Fact]
    [Trait("Spec", "SLP-3")]
    public async Task AnOnlineSlip_SaysPaid_AndShowsNothingToCollect()
    {
        var (host, id, _) = await PackingAsync(postgres, cod: false);
        using var _ = host;

        var slip = (await host.SendAsync(new GetPackingSlipQuery(id)))!;

        Assert.True(slip.IsPaid);
        Assert.Equal(0m, slip.CashToCollect);
    }

    [Fact]
    [Trait("Spec", "SLP-4")]
    public async Task TheExchangeWindowAndSupportContact_ComeFromTheShopSettings()
    {
        var (host, id, _) = await PackingAsync(postgres);
        using var _ = host;
        host.Shop.Settings = new("Little Romp", "923009998877", "care@romp.pk", "Daily 9 to 5", 21);

        var slip = (await host.SendAsync(new GetPackingSlipQuery(id)))!;

        Assert.Equal(("Little Romp", 21, "923009998877", "care@romp.pk", "Daily 9 to 5"), (slip.BrandName, slip.ExchangeWindowDays, slip.SupportWhatsapp, slip.SupportEmail, slip.SupportHours));
    }

    [Fact]
    [Trait("Spec", "SLP-4")]
    public async Task BeforeTheFirstPublish_TheSlipStillPrints_WithTheBrandDefault()
    {
        var (host, id, _) = await PackingAsync(postgres);
        using var _ = host;
        host.Shop.Settings = null;

        var slip = (await host.SendAsync(new GetPackingSlipQuery(id)))!;

        Assert.Equal(("Romp", 0), (slip.BrandName, slip.ExchangeWindowDays));
    }

    [Fact]
    [Trait("Spec", "SLP-5")]
    public async Task PrintingAgain_IsAReprint_WithTheSameContent_AndTheOrderIsUnchanged()
    {
        var (host, id, _) = await PackingAsync(postgres);
        using var _ = host;

        var first = await host.SendAsync(new PrintPackingSlipCommand(id));
        var second = await host.SendAsync(new PrintPackingSlipCommand(id));

        Assert.False(first!.Slip.IsReprint);
        Assert.True(second!.Slip.IsReprint);
        Assert.Equal(first.Slip.Lines, second.Slip.Lines);
        Assert.Equal(first.Slip.Total, second.Slip.Total);
        var order = await Flow.LoadAsync(postgres, id);
        Assert.Equal(2, order.SlipPrintCount);
        Assert.Equal(FulfilmentStatuses.Packing, order.StatusId);
    }

    [Fact]
    [Trait("Spec", "SLP-6")]
    public async Task APrinterThatIsDown_DoesNotBlockThePack_AndTheSlipPrintsOnRetry()
    {
        var (host, id, placed) = await PackingAsync(postgres);
        using var _ = host;
        host.Printer.Up = false;

        var printed = await host.SendAsync(new PrintPackingSlipCommand(id));
        await Flow.PackScanAllAsync(host, postgres, id);
        await host.SendAsync(new RecordWeightCommand(id, FulfilmentHost.Packer, (3 * host.Options.GarmentGrams) + host.Options.PackagingGrams));
        var packed = await host.SendAsync(new CompletePackCommand(id, FulfilmentHost.Packer, null));

        Assert.False(printed!.Printed);
        Assert.True(packed);
        Assert.True((await Flow.LoadAsync(postgres, id)).SlipPrintPending);
        Assert.Empty(host.Printer.Printed);

        host.Printer.Up = true;
        var retried = await host.SendAsync(new RetryPendingSlipsCommand());

        Assert.True(retried >= 1);
        var order = await Flow.LoadAsync(postgres, id);
        Assert.False(order.SlipPrintPending);
        Assert.Equal(1, order.SlipPrintCount);
        Assert.Equal(placed.OrderNumber, Assert.Single(host.Printer.Printed).OrderNumber);
    }

    [Fact]
    [Trait("Spec", "SLP-6")]
    public async Task APrinterThatThrows_IsAFailedPrint_NotAFailedPack()
    {
        var (host, id, _) = await PackingAsync(postgres);
        using var _ = host;
        host.Printer.Up = false;

        var printed = await host.SendAsync(new PrintPackingSlipCommand(id));

        Assert.False(printed!.Printed);
        Assert.True(printed.Slip.PrintPending);
    }

    [Fact]
    [Trait("Spec", "SLP-7")]
    public async Task TheSlip_ShowsTheRecipientNameAndCity_ButNoPhoneEmailOrStreet()
    {
        var (host, id, _) = await PackingAsync(postgres);
        using var _ = host;

        var slip = (await host.SendAsync(new GetPackingSlipQuery(id)))!;

        Assert.Equal(("Sara Khan", "Lahore"), (slip.RecipientName, slip.City));
        var json = JsonSerializer.Serialize(slip);
        Assert.DoesNotContain("0300", json);
        Assert.DoesNotContain("@example", json);
        Assert.DoesNotContain("House", json);
    }

    [Fact]
    [Trait("Spec", "SLP-1")]
    public async Task ASlipIsOnlyAvailableOnceTheOrderIsAtOrPastThePackStation()
    {
        using var host = new FulfilmentHost(postgres);
        var placed = host.PlaceOrder(true, (1, "A-01-1", 5));
        var id = await Seed.CreateAsync(host, placed.OrderId, postgres);

        Assert.Null(await host.SendAsync(new GetPackingSlipQuery(id)));
        Assert.Null(await host.SendAsync(new PrintPackingSlipCommand(id)));
        Assert.Null(await host.SendAsync(new GetPackingSlipQuery(404_404_404)));
        await using var db = postgres.NewContext();
        Assert.Equal(0, (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == id)).SlipPrintCount);
    }
}
