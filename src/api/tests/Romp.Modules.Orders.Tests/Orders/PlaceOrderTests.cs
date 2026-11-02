using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Customers.Contracts;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class PlaceOrderTests(OrdersPostgres postgres) : IClassFixture<OrdersPostgres>
{
    private static string NewKey() => Guid.NewGuid().ToString("N");

    private static CheckoutContact Contact(bool optIn = true, string? email = "ayesha@example.com") => new("Ayesha Khan", email, "0300 1234567", optIn);

    private static CheckoutAddress Address(short city = 7, short province = 2) =>
        new("Ayesha Khan", "0300 1234567", "House 4, Street 2", null, province, city, "54000", "Ring the bell");

    private static PlaceOrderCommand Place(string? key = null, short method = PaymentMethods.CashOnDelivery, CheckoutContact? contact = null, CheckoutAddress? address = null, long? saved = null) =>
        new(key ?? NewKey(), contact ?? Contact(), saved is null ? address ?? Address() : null, saved, method);

    private async Task<Order> LoadAsync(string number)
    {
        await using var db = postgres.NewContext();
        return await db.Orders.Include(o => o.Lines).Include(o => o.History).SingleAsync(o => o.OrderNumber == number);
    }

    [Fact]
    [Trait("Spec", "ORD-1")]
    [Trait("Spec", "CHK-1")]
    public async Task AValidCashOnDeliveryOrder_IsPlaced_WithServerPricesAndSnapshots()
    {
        using var host = new OrdersHost(postgres);
        host.Pricing.Priced = FakePricing.Basket();

        var result = await host.SendAsync(Place());

        Assert.Equal(PlaceOrderOutcome.Placed, result.Outcome);
        Assert.False(result.WasRepeat);
        var order = await LoadAsync(result.OrderNumber!);
        Assert.Equal(OrderStatuses.PendingConfirmation, order.StatusId);
        Assert.Equal((2900m, 0m, 250m, 3150m), (order.Subtotal, order.Discount, order.Shipping, order.Total));
        Assert.Equal(("+923001234567", "Lahore", "Punjab", true), (order.ContactMobile, order.CityName, order.ProvinceName, order.WhatsAppOptIn));
        Assert.Equal(["ROMP-S11", "ROMP-S12"], order.Lines.OrderBy(l => l.VariantId).Select(l => l.Sku));
        Assert.Single(order.History);
        Assert.Single(host.Reservations.Reserved);
        Assert.Equal(order.PlacementKey, host.Reservations.Reserved[0].Key);
    }

    [Fact]
    [Trait("Spec", "ORD-1")]
    public async Task AnOnlineOrder_StartsPaymentPending()
    {
        using var host = new OrdersHost(postgres);

        var result = await host.SendAsync(Place(method: PaymentMethods.Card));

        Assert.Equal(OrderStatuses.PaymentPending, (await LoadAsync(result.OrderNumber!)).StatusId);
    }

    [Fact]
    [Trait("Spec", "ORD-1")]
    [Trait("Spec", "CHK-10")]
    public async Task TheSameKeyTwice_GivesTheFirstOrder_AndReservesOnce()
    {
        using var host = new OrdersHost(postgres);
        var key = NewKey();

        var first = await host.SendAsync(Place(key));
        var again = await host.SendAsync(Place(key));

        Assert.Equal(first.OrderNumber, again.OrderNumber);
        Assert.True(again.WasRepeat);
        Assert.Single(host.Reservations.Reserved);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Orders.CountAsync(o => o.OrderNumber == first.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "ORD-1")]
    public async Task TheSameKeyFromAnotherCaller_IsAnotherOrder()
    {
        using var host = new OrdersHost(postgres);
        var key = NewKey();

        var mine = await host.SendAsync(Place(key));
        host.Pricing.CallerKey = "C2";
        var theirs = await host.SendAsync(Place(key));

        Assert.NotEqual(mine.OrderNumber, theirs.OrderNumber);
    }

    [Fact]
    [Trait("Spec", "ORD-3")]
    public async Task WhenStockIsShort_NothingIsPlaced_TheDiscountHoldIsReleased_AndTheShortVariantsAreNamed()
    {
        using var host = new OrdersHost(postgres);
        host.Redemptions.Hold = new DiscountHold(true, "WELCOME10", 0m, null);
        host.Reservations.Result = new ReservationResult(ReservationOutcome.Shortage, null, [new ShortageLine(11, 2, 1)]);
        var key = NewKey();

        var result = await host.SendAsync(Place(key));

        Assert.Equal((PlaceOrderOutcome.Refused, PlaceOrderRefusal.OutOfStock), (result.Outcome, result.Refusal));
        Assert.Equal([11L], result.ShortVariantIds);
        Assert.Contains("Only 1 of Dress 11", Assert.Single(result.Messages), StringComparison.Ordinal);
        Assert.Single(host.Redemptions.Released);
        await using var db = postgres.NewContext();
        Assert.False(await db.Orders.AnyAsync(o => o.PlacementKey == host.Redemptions.Held[0]));
    }

    [Fact]
    [Trait("Spec", "ORD-4")]
    public async Task EveryAmountComesFromThePricing_NotFromTheRequest()
    {
        using var host = new OrdersHost(postgres);
        host.Pricing.Priced = FakePricing.Basket(0m, "7", (21L, 1, 999m));

        var result = await host.SendAsync(Place());

        var order = await LoadAsync(result.OrderNumber!);
        Assert.Equal((999m, 1249m), (order.Subtotal, order.Total));
        Assert.Equal(21L, Assert.Single(host.Reservations.Reserved[0].Lines).VariantId);
    }

    [Fact]
    [Trait("Spec", "ORD-11")]
    public async Task AnEmptyOrUnbuyableBasket_IsRefused_AndNothingIsHeld()
    {
        using var host = new OrdersHost(postgres);
        host.Pricing.CallerKey = null;
        var noBasket = await host.SendAsync(Place());

        host.Pricing.CallerKey = "C1";
        host.Pricing.Priced = new CheckoutPricing([], 0m, 0m, 0m, false, 0m, null, false, ["Your bag is empty."]);
        var empty = await host.SendAsync(Place());

        host.Pricing.Priced = FakePricing.Basket() with { CanOrder = false, Problems = ["Dress 11 is out of stock."] };
        var problems = await host.SendAsync(Place());

        Assert.All(new[] { noBasket, empty }, r => Assert.Equal(PlaceOrderRefusal.EmptyBasket, r.Refusal));
        Assert.Equal((PlaceOrderRefusal.BasketProblems, "Dress 11 is out of stock."), (problems.Refusal, problems.Messages[0]));
        Assert.Empty(host.Reservations.Reserved);
        Assert.Empty(host.Redemptions.Held);
    }

    [Fact]
    [Trait("Spec", "ORD-3")]
    public async Task ARejectedCode_RefusesTheOrder_BeforeAnyStockIsReserved()
    {
        using var host = new OrdersHost(postgres);
        host.Redemptions.Hold = new DiscountHold(false, "OLD", 0m, "That code has expired.");

        var result = await host.SendAsync(Place());

        Assert.Equal((PlaceOrderRefusal.CodeRejected, "That code has expired."), (result.Refusal, result.Messages[0]));
        Assert.Empty(host.Reservations.Reserved);
    }

    [Fact]
    [Trait("Spec", "ORD-7")]
    public async Task IfTheOrderCannotBeWritten_TheReservationAndTheHoldAreGivenBack()
    {
        using var host = new OrdersHost(postgres);
        host.Redemptions.Hold = new DiscountHold(true, "WELCOME10", 0m, null);
        // A basket total that the order itself refuses (a discount larger than the subtotal) fails inside the Orders transaction.
        host.Pricing.Priced = FakePricing.Basket(discount: 99_999m);
        host.Redemptions.Hold = new DiscountHold(true, "WELCOME10", 99_999m, null);

        await Assert.ThrowsAnyAsync<Exception>(() => host.SendAsync(Place()));

        Assert.Single(host.Reservations.Released);
        Assert.Single(host.Redemptions.Released);
        await using var db = postgres.NewContext();
        Assert.False(await db.Orders.AnyAsync(o => o.PlacementKey == host.Reservations.Released[0]));
    }

    [Fact]
    [Trait("Spec", "ORD-12")]
    [Trait("Spec", "CHK-6")]
    public async Task AGuestWithNothingVerified_CanOrder()
    {
        using var host = new OrdersHost(postgres);
        host.Customer.CustomerId = null;
        host.Pricing.CallerKey = "Gabc123";

        var result = await host.SendAsync(Place());

        var order = await LoadAsync(result.OrderNumber!);
        Assert.Null(order.CustomerId);
        Assert.Equal("ayesha@example.com", order.ContactEmail);
    }

    [Fact]
    [Trait("Spec", "CHK-12")]
    public async Task TheWhatsAppOptIn_IsStoredWithTheOrderAndItsTime()
    {
        using var host = new OrdersHost(postgres);

        var withOptIn = await LoadAsync((await host.SendAsync(Place(contact: Contact(optIn: true)))).OrderNumber!);
        var without = await LoadAsync((await host.SendAsync(Place(contact: Contact(optIn: false)))).OrderNumber!);

        Assert.True(withOptIn.WhatsAppOptIn);
        Assert.NotNull(withOptIn.WhatsAppOptInAt);
        Assert.False(without.WhatsAppOptIn);
        Assert.Null(without.WhatsAppOptInAt);
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public async Task ASavedAddress_IsUsedForTheSignedInCustomer_AndSomeoneElsesIsNotFound()
    {
        using var host = new OrdersHost(postgres);
        host.Addresses.Mine[5] = new CustomerAddressSummary(5, "Sara Ali", "+923217654321", 2, 7, "Flat 9", null, "54000", true);

        var mine = await host.SendAsync(Place(saved: 5));
        var other = await host.SendAsync(Place(saved: 99));
        host.Customer.CustomerId = null;
        var guest = await host.SendAsync(Place(saved: 5));

        var order = await LoadAsync(mine.OrderNumber!);
        Assert.Equal(("Sara Ali", "+923217654321", "Flat 9"), (order.RecipientName, order.RecipientPhone, order.AddressLine1));
        Assert.Equal(PlaceOrderRefusal.AddressNotFound, other.Refusal);
        Assert.Equal(PlaceOrderRefusal.AddressNotFound, guest.Refusal);
    }

    [Fact]
    [Trait("Spec", "CHK-8")]
    public async Task ACityThatIsNotInTheProvince_IsRefused()
    {
        using var host = new OrdersHost(postgres);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Place(address: Address(city: 8))));

        Assert.True(ex.Errors.ContainsKey("cityId"));
    }

    [Theory]
    [Trait("Spec", "CHK-2")]
    [Trait("Spec", "CHK-3")]
    [InlineData("", "0300 1234567", "Enter your name.")]
    [InlineData("Ayesha", "", "Enter your mobile number.")]
    [InlineData("Ayesha", "abc", "Enter a Pakistani mobile number such as 0300 1234567.")]
    public async Task ABlankOrBadContactField_IsRefusedWithAMessage(string name, string mobile, string message)
    {
        using var host = new OrdersHost(postgres);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Place(contact: new CheckoutContact(name, "a@b.co", mobile, false))));

        Assert.Contains(message, ex.Errors.Values.SelectMany(v => v));
    }

    [Theory]
    [Trait("Spec", "ORD-1")]
    [InlineData("short")]
    [InlineData("has space in it, which is not allowed")]
    public async Task ABadSubmitKey_IsRefused(string key)
    {
        using var host = new OrdersHost(postgres);

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Place(key)));
    }

    [Fact]
    [Trait("Spec", "ORD-8")]
    public async Task ThePlacedEvent_WaitsInTheOutbox_WithNoPersonalData()
    {
        using var host = new OrdersHost(postgres);

        var result = await host.SendAsync(Place());

        await using var db = postgres.NewContext();
        var message = db.Set<OutboxMessage>().Where(m => m.EventType == "OrderPlacedEvent").AsEnumerable().Single(m => m.Payload.Contains(result.OrderNumber!, StringComparison.Ordinal));
        Assert.Contains("\"PaymentMethodCode\": \"COD\"", message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("Ayesha", message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("0300", message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", message.Payload, StringComparison.Ordinal);
    }
}
