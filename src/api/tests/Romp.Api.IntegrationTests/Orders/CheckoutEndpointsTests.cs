using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Api.IntegrationTests.Orders;

/// <summary>
/// Placing an order through the real HTTP API against a real PostgreSQL (orders H1, ORD-1, CHK-1, CHK-10): a guest fills a basket, places a
/// cash on delivery order, repeats the submit, and is refused in words when stock is short. Catalog and Warehouse are replaced by in-memory
/// fakes (the reservation itself is proved against PostgreSQL in the Warehouse tests); Cart and Orders are real.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class CheckoutEndpointsTests(ProductionApiFixture api)
{
    private const string Origin = "http://localhost";
    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    private sealed class FakeSales : IVariantSalesQueries
    {
        public Dictionary<long, VariantSale> Variants { get; } = [];

        public Task<IReadOnlyList<VariantSale>> GetManyAsync(IReadOnlyCollection<long> variantIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VariantSale>>([.. variantIds.Where(Variants.ContainsKey).Select(id => Variants[id])]);
    }

    private sealed class FakeAvailability : IStockAvailabilityQueries
    {
        public Dictionary<long, int> Available { get; } = [];

        public Task<IReadOnlyList<StockAvailability>> GetManyAsync(IReadOnlyCollection<long> variantIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StockAvailability>>([.. variantIds.Where(Available.ContainsKey).Select(id => new StockAvailability(id, $"SKU-{id}", Available[id], 1))]);

        public Task<IReadOnlyList<StockAvailability>> ListAvailabilityAsync(long afterVariantId, int take, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<StockAvailability?> GetAsync(long variantId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeReservations : IStockReservations
    {
        public List<string> Reserved { get; } = [];

        public ReservationResult? Refuse { get; set; }

        public Task<ReservationResult> ReserveAsync(string placementKey, IReadOnlyList<ReservationLineRequest> lines, CancellationToken cancellationToken)
        {
            Reserved.Add(placementKey);
            return Task.FromResult(Refuse ?? new ReservationResult(ReservationOutcome.Reserved, 1, []));
        }

        public Task ReleaseAsync(string placementKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private (WebApplicationFactory<Program> Factory, FakeSales Sales, FakeAvailability Stock, FakeReservations Reservations) Shop()
    {
        var sales = new FakeSales();
        var stock = new FakeAvailability();
        var reservations = new FakeReservations();
        var factory = api.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IVariantSalesQueries>(sales);
            services.AddSingleton<IStockAvailabilityQueries>(stock);
            services.AddSingleton<IStockReservations>(reservations);
        }));
        return (factory, sales, stock, reservations);
    }

    private static long NewVariant(FakeSales sales, FakeAvailability stock, decimal price = 1000m, int available = 10)
    {
        var id = Random.Shared.NextInt64(1_000_000, 900_000_000);
        sales.Variants[id] = new VariantSale(
            id, id + 1, "Floral Dress", "/girls/dresses/floral-dress", "Red", "2-3Y", $"ROMP-S{id}", id + 2, price, null, true, 3, id + 3, "images/a.jpg", "A red dress", 40, 60);
        stock.Available[id + 2] = available;
        return id;
    }

    private static HttpClient Guest(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Dev-Customer", "none");
        return client;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body = null, string? key = null, string? origin = Origin)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return client.SendAsync(request);
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    private static string NewKey() => Guid.NewGuid().ToString("N");

    private static async Task<(short ProvinceId, short CityId)> AnyCityAsync(HttpClient client)
    {
        var provinces = await BodyAsync(await client.GetAsync("/api/geography/provinces"));
        var provinceId = provinces[0].GetProperty("id").GetInt16();
        var cities = await BodyAsync(await client.GetAsync($"/api/geography/provinces/{provinceId}/cities"));
        return (provinceId, cities[0].GetProperty("id").GetInt16());
    }

    private static object OrderIn((short ProvinceId, short CityId) place, string method = "COD") => new
    {
        contact = new { name = "Ayesha Khan", email = "ayesha@example.com", mobile = "0300 1234567", whatsAppOptIn = true },
        address = new { recipientName = "Ayesha Khan", recipientPhone = "0300 1234567", lineOne = "House 4, Street 2", provinceId = place.ProvinceId, cityId = place.CityId, postcode = "54000" },
        paymentMethod = method,
    };

    [Fact]
    [Trait("Spec", "CHK-9")]
    public async Task TheSummary_ShowsTheServerFigures_AndOnlyCashOnDeliveryUntilGatewaysAreOn()
    {
        var (factory, sales, stock, _) = Shop();
        var variant = NewVariant(sales, stock, 1250m);
        using var client = Guest(factory);
        await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 2 });
        var place = await AnyCityAsync(client);

        var summary = await BodyAsync(await client.GetAsync($"/api/orders/checkout?cityId={place.CityId}&method=cod"));

        Assert.Equal(2500m, summary.GetProperty("subtotal").GetDecimal());
        Assert.True(summary.GetProperty("canOrder").GetBoolean());
        Assert.Equal(["COD"], summary.GetProperty("paymentMethods").EnumerateArray().Select(m => m.GetProperty("code").GetString()));
        Assert.Equal(0, summary.GetProperty("savedAddresses").GetArrayLength());
    }

    [Fact]
    [Trait("Spec", "CHK-1")]
    [Trait("Spec", "ORD-1")]
    public async Task AGuest_PlacesACashOnDeliveryOrder_ThenTheSameSubmitAgain_GivesTheSameOrder()
    {
        var (factory, sales, stock, reservations) = Shop();
        var variant = NewVariant(sales, stock, 1250m);
        using var client = Guest(factory);
        await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 2 });
        var place = await AnyCityAsync(client);
        var key = NewKey();

        var first = await Send(client, HttpMethod.Post, "/api/orders", OrderIn(place), key);
        var again = await Send(client, HttpMethod.Post, "/api/orders", OrderIn(place), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var number = (await BodyAsync(first)).GetProperty("orderNumber").GetString();
        Assert.Matches(@"^RMP-\d{4}-\d{5}-\d$", number!);
        Assert.Equal(number, (await BodyAsync(again)).GetProperty("orderNumber").GetString());
        Assert.Single(reservations.Reserved);
    }

    [Fact]
    [Trait("Spec", "ORD-4")]
    public async Task APriceInTheRequestBody_HasNowhereToLand()
    {
        var (factory, sales, stock, _) = Shop();
        var variant = NewVariant(sales, stock, 1250m);
        using var client = Guest(factory);
        await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 1 });
        var place = await AnyCityAsync(client);
        var tampered = new
        {
            contact = new { name = "Ayesha Khan", email = "a@example.com", mobile = "0300 1234567", whatsAppOptIn = false },
            address = new { recipientName = "Ayesha Khan", recipientPhone = "0300 1234567", lineOne = "House 4", provinceId = place.ProvinceId, cityId = place.CityId },
            paymentMethod = "COD",
            total = 1,
            discount = 1249,
            unitPrice = 1,
        };

        var response = await Send(client, HttpMethod.Post, "/api/orders", tampered, NewKey());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "ORD-3")]
    public async Task WhenStockIsShort_TheOrderIsRefusedInWords_AndTheBasketIsKept()
    {
        var (factory, sales, stock, reservations) = Shop();
        var variant = NewVariant(sales, stock, 1250m);
        using var client = Guest(factory);
        await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 2 });
        var place = await AnyCityAsync(client);
        reservations.Refuse = new ReservationResult(ReservationOutcome.Shortage, null, [new ShortageLine(variant + 2, 2, 1)]);

        var response = await Send(client, HttpMethod.Post, "/api/orders", OrderIn(place), NewKey());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Equal("OutOfStock", body.GetProperty("reason").GetString());
        Assert.Contains("Only 1 of Floral Dress", body.GetProperty("messages")[0].GetString(), StringComparison.Ordinal);
        Assert.Equal(variant, body.GetProperty("shortVariantIds")[0].GetInt64());
        var cart = await BodyAsync(await client.GetAsync("/api/cart"));
        Assert.Equal(2, cart.GetProperty("itemCount").GetInt32());
    }

    [Fact]
    [Trait("Spec", "ORD-11")]
    public async Task AnEmptyBasket_CannotBeOrdered()
    {
        var (factory, _, _, _) = Shop();
        using var client = Guest(factory);
        var place = await AnyCityAsync(client);

        var response = await Send(client, HttpMethod.Post, "/api/orders", OrderIn(place), NewKey());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("EmptyBasket", (await BodyAsync(response)).GetProperty("reason").GetString());
    }

    [Theory]
    [Trait("Spec", "CHK-2")]
    [InlineData(null)]
    [InlineData("short")]
    public async Task AMissingOrBadSubmitKey_IsRefused(string? key)
    {
        var (factory, sales, stock, _) = Shop();
        var variant = NewVariant(sales, stock);
        using var client = Guest(factory);
        await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 1 });
        var place = await AnyCityAsync(client);

        var response = await Send(client, HttpMethod.Post, "/api/orders", OrderIn(place), key);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "CHK-3")]
    public async Task ALetterInThePhone_GetsAnInlineMessage()
    {
        var (factory, sales, stock, _) = Shop();
        var variant = NewVariant(sales, stock);
        using var client = Guest(factory);
        await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 1 });
        var place = await AnyCityAsync(client);
        var bad = new
        {
            contact = new { name = "Ayesha", email = "a@example.com", mobile = "03OO1234567", whatsAppOptIn = false },
            address = new { recipientName = "Ayesha", recipientPhone = "0300 1234567", lineOne = "House 4", provinceId = place.ProvinceId, cityId = place.CityId },
            paymentMethod = "COD",
        };

        var response = await Send(client, HttpMethod.Post, "/api/orders", bad, NewKey());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Pakistani mobile", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "SEC-12")]
    public async Task AnotherSite_CannotPlaceAnOrder()
    {
        var (factory, sales, stock, _) = Shop();
        var variant = NewVariant(sales, stock);
        using var client = Guest(factory);
        await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 1 });
        var place = await AnyCityAsync(client);

        var response = await Send(client, HttpMethod.Post, "/api/orders", OrderIn(place), NewKey(), "https://evil.example");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "PAY-7")]
    public async Task AnOnlineMethod_IsRefused_WhileNoGatewayIsSwitchedOn()
    {
        var (factory, sales, stock, reservations) = Shop();
        var variant = NewVariant(sales, stock);
        using var client = Guest(factory);
        await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 1 });
        var place = await AnyCityAsync(client);

        var response = await Send(client, HttpMethod.Post, "/api/orders", OrderIn(place, "CARD"), NewKey());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(reservations.Reserved);
    }
}
