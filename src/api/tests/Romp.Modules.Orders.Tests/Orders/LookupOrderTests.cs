using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Application.OrderLookup;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Tests.Support;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class LookupOrderTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static int _seq;

    private static string Ip() => $"ip-{Interlocked.Increment(ref _seq)}-{Guid.NewGuid():N}";

    private static string UniqueMobile() => $"03{Interlocked.Increment(ref _seq) + 300_000_000:000000000}";

    private static async Task<(string Number, string Mobile)> PlacedAsync(OrdersHost host)
    {
        var mobile = UniqueMobile();
        var result = await host.SendAsync(new PlaceOrderCommand(
            Guid.NewGuid().ToString("N"),
            new CheckoutContact("Ayesha Khan", "a@example.com", mobile, false),
            new CheckoutAddress("Ayesha Khan", mobile, "House 4, Street 2", null, 2, 7, "54000", null), null, PaymentMethods.CashOnDelivery));
        return (result.OrderNumber!, mobile);
    }

    [Fact]
    [Trait("Spec", "OLK-1")]
    public async Task ACorrectNumberAndMobile_ShowsTheStatusItemsAndTotals()
    {
        using var host = new OrdersHost(postgres);
        var (number, mobile) = await PlacedAsync(host);

        var result = await host.SendAsync(new LookupOrderQuery(number, mobile, null, Ip()));

        Assert.Equal(LookupStatus.Found, result.Status);
        var order = result.Order!;
        Assert.Equal(number, order.OrderNumber);
        Assert.Equal("PNDC", order.StatusCode);
        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(order.Subtotal - order.Discount + order.Shipping, order.Total);
    }

    [Theory]
    [Trait("Spec", "OLK-7")]
    [InlineData("0300 1234567")]
    [InlineData("0300-1234567")]
    [InlineData("+92 300 1234567")]
    [InlineData("923001234567")]
    public async Task TheMobile_IsComparedInOneFormat_HoweverItIsTyped(string typed)
    {
        using var host = new OrdersHost(postgres);
        var mobile = "0300" + $"{Interlocked.Increment(ref _seq) + 1_000_000:0000000}"[^7..];
        var placed = await host.SendAsync(new PlaceOrderCommand(
            Guid.NewGuid().ToString("N"),
            new CheckoutContact("Ayesha Khan", null, mobile, false),
            new CheckoutAddress("Ayesha Khan", mobile, "House 4", null, 2, 7, null, null), null, PaymentMethods.CashOnDelivery));

        var same = typed switch
        {
            "0300 1234567" => $"{mobile[..4]} {mobile[4..]}",
            "0300-1234567" => $"{mobile[..4]}-{mobile[4..]}",
            "+92 300 1234567" => $"+92 {mobile[1..4]} {mobile[4..]}",
            _ => "92" + mobile[1..],
        };

        var result = await host.SendAsync(new LookupOrderQuery(placed.OrderNumber, same, null, Ip()));

        Assert.Equal(LookupStatus.Found, result.Status);
    }

    [Fact]
    [Trait("Spec", "OLK-2")]
    public async Task AWrongNumber_AWrongMobile_AndBoth_GiveTheSameAnswer()
    {
        using var host = new OrdersHost(postgres);
        var (number, mobile) = await PlacedAsync(host);
        var other = OrderNumbers.Format(2026, 99_999);

        var wrongMobile = await host.SendAsync(new LookupOrderQuery(number, "03009999999", null, Ip()));
        var wrongNumber = await host.SendAsync(new LookupOrderQuery(other, mobile, null, Ip()));
        var both = await host.SendAsync(new LookupOrderQuery(other, "03009999999", null, Ip()));
        var junk = await host.SendAsync(new LookupOrderQuery("hello", "not a number", null, Ip()));
        var empty = await host.SendAsync(new LookupOrderQuery(null, null, null, Ip()));

        Assert.All([wrongMobile, wrongNumber, both, junk, empty], r => Assert.Same(LookupResult.Mismatch, r));
    }

    [Fact]
    [Trait("Spec", "OLK-6")]
    public async Task TheAnswer_NeverHoldsTheFullAddressOrThePhone()
    {
        using var host = new OrdersHost(postgres);
        var (number, mobile) = await PlacedAsync(host);

        var result = await host.SendAsync(new LookupOrderQuery(number, mobile, null, Ip()));

        var json = JsonSerializer.Serialize(result.Order);
        Assert.DoesNotContain("House 4", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Ayesha", json, StringComparison.Ordinal);
        Assert.DoesNotContain(mobile, json, StringComparison.Ordinal);
        Assert.DoesNotContain(mobile[1..], json, StringComparison.Ordinal);
        Assert.DoesNotContain("a@example.com", json, StringComparison.Ordinal);
        Assert.Equal("Lahore", result.Order!.City);
        Assert.Contains("XX-XXX", result.Order.MaskedMobile, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "OLK-4")]
    public async Task AfterTwoFailures_TheThirdAttemptNeedsTheCaptcha_AndPassingItWorks()
    {
        using var host = new OrdersHost(postgres);
        var (number, mobile) = await PlacedAsync(host);
        var ip = Ip();

        await host.SendAsync(new LookupOrderQuery(number, "03001111111", null, ip));
        await host.SendAsync(new LookupOrderQuery(number, "03001111112", null, ip));
        Assert.Equal(0, host.Captcha.Asked);

        var third = await host.SendAsync(new LookupOrderQuery(number, "03001111113", null, ip));
        var noToken = await host.SendAsync(new LookupOrderQuery(number, mobile, null, ip));
        var passed = await host.SendAsync(new LookupOrderQuery(number, mobile, FakeCaptcha.PassToken, ip));

        Assert.Same(LookupResult.Mismatch, third);
        Assert.Equal(LookupStatus.CaptchaRequired, noToken.Status);
        Assert.Equal(LookupStatus.Found, passed.Status);
    }

    [Fact]
    [Trait("Spec", "OLK-4")]
    public async Task ASuccess_ClearsTheFailureCount()
    {
        using var host = new OrdersHost(postgres);
        var (number, mobile) = await PlacedAsync(host);
        var ip = Ip();

        await host.SendAsync(new LookupOrderQuery(number, "03001111111", null, ip));
        await host.SendAsync(new LookupOrderQuery(number, "03001111112", null, ip));
        await host.SendAsync(new LookupOrderQuery(number, mobile, null, ip));
        var next = await host.SendAsync(new LookupOrderQuery(number, "03001111113", null, ip));

        Assert.Same(LookupResult.Mismatch, next);
        Assert.Equal(0, host.Captcha.Asked);
    }

    [Fact]
    [Trait("Spec", "OLK-3")]
    public async Task AnAddress_IsCutOffAfterTwentyAttemptsAnHour_AndFreedWhenTheyAgeOut()
    {
        using var host = new OrdersHost(postgres);
        var (number, mobile) = await PlacedAsync(host);
        var ip = Ip();
        for (var i = 0; i < LookupThrottle.MaxAttemptsPerWindow; i++)
        {
            await host.SendAsync(new LookupOrderQuery(number, mobile, null, ip));
        }

        var cut = await host.SendAsync(new LookupOrderQuery(number, mobile, null, ip));
        var otherAddress = await host.SendAsync(new LookupOrderQuery(number, mobile, null, Ip()));
        host.Time.Now = host.Time.Now.AddMinutes(61);
        var later = await host.SendAsync(new LookupOrderQuery(number, mobile, null, ip));

        Assert.Equal(LookupStatus.RateLimited, cut.Status);
        Assert.Equal(LookupStatus.Found, otherAddress.Status);
        Assert.Equal(LookupStatus.Found, later.Status);
    }

    [Fact]
    [Trait("Spec", "OLK-3")]
    public async Task ARateLimitedAttempt_IsNotRecordedAgain()
    {
        using var host = new OrdersHost(postgres);
        var (number, mobile) = await PlacedAsync(host);
        var ip = Ip();
        for (var i = 0; i < LookupThrottle.MaxAttemptsPerWindow + 3; i++)
        {
            await host.SendAsync(new LookupOrderQuery(number, mobile, null, ip));
        }

        await using var db = postgres.NewContext();
        var hash = new FakeAddressHasher().Hash(ip);
        Assert.Equal(LookupThrottle.MaxAttemptsPerWindow, await db.LookupAttempts.CountAsync(a => a.AddressHash == hash));
    }

    [Fact]
    [Trait("Spec", "OLK-2")]
    [Trait("Spec", "SEC-25")]
    public async Task TheAttemptRecord_HoldsNoNumberAndNoMobile()
    {
        using var host = new OrdersHost(postgres);
        var (number, _) = await PlacedAsync(host);
        var ip = Ip();

        await host.SendAsync(new LookupOrderQuery(number, "03001234567", null, ip));

        await using var db = postgres.NewContext();
        var hash = new FakeAddressHasher().Hash(ip);
        var row = await db.LookupAttempts.SingleAsync(a => a.AddressHash == hash);
        Assert.True(row.Failed);
        Assert.DoesNotContain(number, row.AddressHash, StringComparison.Ordinal);
    }
}
