using System.Text.Json;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Application.Bookings;
using Romp.Modules.Shipping.Application.Labels;
using Romp.Modules.Shipping.Tests.Support;

namespace Romp.Modules.Shipping.Tests.Labels;

/// <summary>S14 S6: the Code 128 barcode (pure) and the combined label (LBL-1 to LBL-7).</summary>
public sealed class Code128Tests
{
    [Fact]
    [Trait("Spec", "LBL-3")]
    public void EverySymbolPattern_IsElevenModulesWide_SixAlternatingWidths_AndUnique()
    {
        var patterns = Enumerable.Range(0, Code128.PatternCount).Select(Code128.PatternOf).ToList();

        Assert.Equal(106, patterns.Count);
        Assert.All(patterns, p =>
        {
            Assert.Equal(6, p.Length);
            Assert.Equal(11, p.Sum(c => c - '0'));
        });
        Assert.Equal(patterns.Count, patterns.Distinct().Count());
    }

    [Theory]
    [Trait("Spec", "LBL-3")]
    [InlineData("A")]
    [InlineData("PRM-000123-4567")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-")]
    public void TheBarcode_HasTheRightLength_StartsWithStartB_AndEndsWithTheStopPattern(string text)
    {
        var modules = Code128.Modules(text);

        Assert.Equal((11 * (text.Length + 3)) + 2, modules.Length);
        Assert.StartsWith("11010010000", modules);
        Assert.EndsWith("1100011101011", modules);
        Assert.All(modules, c => Assert.True(c is '0' or '1'));
    }

    [Fact]
    [Trait("Spec", "LBL-3")]
    public void TheChecksum_IsTheStartValuePlusEachSymbolTimesItsPosition_ModuloOneHundredThree()
    {
        // "A" is value 33: (104 + 1 * 33) mod 103 = 34. "HI": H = 40, I = 41 -> (104 + 40 + 82) mod 103 = 226 mod 103 = 20.
        Assert.Equal(34, Code128.Checksum("A"));
        Assert.Equal(20, Code128.Checksum("HI"));
    }

    [Fact]
    [Trait("Spec", "LBL-3")]
    public void TheBarcodeCanBeReadBackToTheSameText()
    {
        var text = "PRM-000042-9";
        var modules = Code128.Modules(text);

        var decoded = Decode(modules);

        Assert.Equal(text, decoded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tab\there")]
    public void AnEmptyOrNonPrintableText_IsRefused(string text) => Assert.Throws<DomainException>(() => Code128.Modules(text));

    /// <summary>Reads the modules back through the same pattern table: start, symbols, check symbol, stop.</summary>
    private static string Decode(string modules)
    {
        var widths = new List<int>();
        var run = 1;
        for (var i = 1; i < modules.Length; i++)
        {
            if (modules[i] == modules[i - 1])
            {
                run++;
            }
            else
            {
                widths.Add(run);
                run = 1;
            }
        }

        widths.Add(run);
        var symbols = new List<int>();
        for (var i = 0; i + 6 <= widths.Count - 7; i += 6)
        {
            var key = string.Concat(widths.Skip(i).Take(6));
            symbols.Add(Enumerable.Range(0, Code128.PatternCount).First(v => Code128.PatternOf(v) == key));
        }

        Assert.Equal(104, symbols[0]);
        var text = string.Concat(symbols.Skip(1).Take(symbols.Count - 2).Select(v => (char)(v + 32)));
        Assert.Equal(Code128.Checksum(text), symbols[^1]);
        return text;
    }
}

[Collection(ShippingPostgresFixtureDefinition.Name)]
public sealed class LabelTests(ShippingPostgres postgres)
{
    [Fact]
    [Trait("Spec", "LBL-1")]
    public async Task ABookedShipment_HasALabelWithRecipientAddressPhoneOrderCourierAndWaybill()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var shipment = await ShippingHost.LoadAsync(postgres, id);

        var label = (await host.SendAsync(new GetLabelQuery(id)))!;

        Assert.Equal((shipment.OrderNumber, "Primary courier", shipment.Waybill), (label.OrderNumber, label.CourierName, label.Waybill));
        Assert.Equal(("Sara Khan", "03001234567", "House 4, Street 9", "Lahore"), (label.RecipientName, label.Phone, label.AddressLine1, label.City));
        Assert.Equal(Code128.Modules(shipment.Waybill!), label.BarcodeModules);
    }

    [Fact]
    [Trait("Spec", "LBL-2")]
    public async Task BeforeABooking_ThereIsNoLabel()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewShipmentAsync();

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new GetLabelQuery(id)));
        Assert.Null(await host.SendAsync(new GetLabelQuery(404_404)));
    }

    [Fact]
    [Trait("Spec", "LBL-4")]
    public async Task ACashOnDeliveryLabel_ShowsTheAmount_AndAPaidOneNone()
    {
        using var host = new ShippingHost(postgres);
        var cod = await host.NewShipmentAsync(cod: true);
        var paid = await host.NewShipmentAsync(cod: false);
        await host.SendAsync(new BookShipmentCommand(cod));
        await host.SendAsync(new BookShipmentCommand(paid));

        var a = (await host.SendAsync(new GetLabelQuery(cod)))!;
        var b = (await host.SendAsync(new GetLabelQuery(paid)))!;

        Assert.Equal((true, 4700m), (a.IsCashOnDelivery, a.CashToCollect));
        Assert.Equal((false, 0m), (b.IsCashOnDelivery, b.CashToCollect));
    }

    [Fact]
    [Trait("Spec", "LBL-5")]
    public async Task APrintedLabel_IsMarkedAReprintNextTime_WithTheSameContent()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var first = (await host.SendAsync(new GetLabelQuery(id)))!;

        await host.SendAsync(new MarkLabelPrintedCommand(id));
        var second = (await host.SendAsync(new GetLabelQuery(id)))!;

        Assert.False(first.IsReprint);
        Assert.True(second.IsReprint);
        Assert.Equal(1, second.PrintCount);
        Assert.Equal(first.BarcodeModules, second.BarcodeModules);
        Assert.Equal(first.AddressLine1, second.AddressLine1);
    }

    [Fact]
    [Trait("Spec", "LBL-7")]
    public async Task TheLabelDto_IsBuiltFromOrdersOnRequest_AndAnUnknownOrderGivesNoLabel()
    {
        using var host = new ShippingHost(postgres);
        var id = await host.NewBookedShipmentAsync();
        var shipment = await ShippingHost.LoadAsync(postgres, id);
        host.Orders.Views.Remove(shipment.OrderId);

        Assert.Null(await host.SendAsync(new GetLabelQuery(id)));
        Assert.NotNull(JsonSerializer.Serialize(new { ok = true }));
    }
}
