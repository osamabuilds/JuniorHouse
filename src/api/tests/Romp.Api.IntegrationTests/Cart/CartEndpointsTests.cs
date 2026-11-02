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

namespace Romp.Api.IntegrationTests.Cart;

/// <summary>
/// The basket through the real HTTP API against a real PostgreSQL (cart C7): guests and the signed-in (stand-in) customer, cookies, the
/// origin check, stock refusals and ownership. Catalog and Warehouse are replaced by in-memory fakes, because what is tested here is the
/// cart, not their data. The default caller is the development stand-in customer; <c>X-Dev-Customer: none</c> makes a guest.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class CartEndpointsTests(ProductionApiFixture api)
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

    private (WebApplicationFactory<Program> Factory, FakeSales Sales, FakeAvailability Stock) Shop()
    {
        var sales = new FakeSales();
        var stock = new FakeAvailability();
        var factory = api.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IVariantSalesQueries>(sales);
            services.AddSingleton<IStockAvailabilityQueries>(stock);
        }));
        return (factory, sales, stock);
    }

    private static long NewVariant(FakeSales sales, FakeAvailability stock, decimal price = 1000m, int available = 10)
    {
        var id = Random.Shared.NextInt64(1_000_000, 900_000_000);
        sales.Variants[id] = new VariantSale(
            id, id + 1, "Floral Dress", "/girls/dresses/floral-dress", "Red", "2-3Y", $"ROMP-S{id}", id + 2, price, null, true, 3, "images/a.jpg", "A red dress", 40, 60);
        stock.Available[id + 2] = available;
        return id;
    }

    private static HttpClient Guest(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Dev-Customer", "none");
        return client;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body = null, string? origin = Origin)
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

        return client.SendAsync(request);
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    private static string[] SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToArray() : [];

    [Fact]
    [Trait("Spec", "SEO-05")]
    public async Task AReadByAGuestWithNoCookie_IsEmpty_CreatesNothing_AndIsNeverCached()
    {
        var (factory, _, _) = Shop();
        using var client = Guest(factory);

        var response = await Send(client, HttpMethod.Get, "/api/cart");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(SetCookies(response));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        var body = await BodyAsync(response);
        Assert.Equal(0, body.GetProperty("itemCount").GetInt32());
        Assert.Equal(0, body.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task AGuestAdds_GetsAnHttpOnlyLaxCookie_AndSeesTheBasketOnTheNextRead()
    {
        var (factory, sales, stock) = Shop();
        var variant = NewVariant(sales, stock, 1250m);
        using var client = Guest(factory);

        var added = await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 2 });
        var read = await Send(client, HttpMethod.Get, "/api/cart");

        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        var cookie = Assert.Single(SetCookies(added), c => c.StartsWith("romp-guest=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        var body = await BodyAsync(read);
        Assert.Equal(2500m, body.GetProperty("subtotal").GetDecimal());
        Assert.Equal(2, body.GetProperty("itemCount").GetInt32());
        Assert.Equal(variant, body.GetProperty("lines")[0].GetProperty("variantId").GetInt64());
    }

    [Fact]
    [Trait("Spec", "CRT-6")]
    public async Task APriceOrTotalInTheBody_IsIgnored_TheServerPriceIsUsed()
    {
        var (factory, sales, stock) = Shop();
        var variant = NewVariant(sales, stock, 1000m);
        using var client = Guest(factory);

        var response = await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 1, price = 1, unitPrice = 1, total = 1, subtotal = 1 });

        var body = await BodyAsync(response);
        Assert.Equal(1000m, body.GetProperty("subtotal").GetDecimal());
        Assert.Equal(1000m, body.GetProperty("lines")[0].GetProperty("unitPrice").GetDecimal());
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task ChangingAndRemovingLines_UpdateTheSubtotal_AndAnEmptyBasketIsEmpty()
    {
        var (factory, sales, stock) = Shop();
        var variant = NewVariant(sales, stock, 400m);
        using var client = Guest(factory);
        await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 1 });

        var changed = await BodyAsync(await Send(client, HttpMethod.Put, $"/api/cart/lines/{variant}", new { quantity = 5 }));
        var removed = await BodyAsync(await Send(client, HttpMethod.Delete, $"/api/cart/lines/{variant}"));

        Assert.Equal(2000m, changed.GetProperty("subtotal").GetDecimal());
        Assert.Equal(0, removed.GetProperty("itemCount").GetInt32());
        Assert.Equal(0, removed.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    [Trait("Spec", "CRT-4")]
    public async Task AddingMoreThanIsInStock_IsRefused_NamingTheCount()
    {
        var (factory, sales, stock) = Shop();
        var variant = NewVariant(sales, stock, 1000m, available: 2);
        using var client = Guest(factory);

        var response = await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 3 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Only 2 are in stock", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Spec", "CRT-5")]
    [InlineData(0)]
    [InlineData(1)]
    public async Task AnUnknownVariant_IsRefused_AndABadQuantityIsRefused(int kind)
    {
        var (factory, sales, stock) = Shop();
        var variant = NewVariant(sales, stock);
        using var client = Guest(factory);

        var response = kind == 0
            ? await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = 424242, quantity = 1 })
            : await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 0 });

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict, response.StatusCode.ToString());
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public async Task AWriteFromAnotherSite_IsForbidden_AndAWriteWithNoOriginIsLeftToTheOtherDefences()
    {
        var (factory, sales, stock) = Shop();
        var variant = NewVariant(sales, stock);
        using var client = Guest(factory);

        var cross = await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 1 }, origin: "http://evil.example");
        var none = await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 1 }, origin: null);

        Assert.Equal(HttpStatusCode.Forbidden, cross.StatusCode);
        Assert.Equal(HttpStatusCode.OK, none.StatusCode);
    }

    [Fact]
    [Trait("Spec", "CRT-9")]
    public async Task AnotherBrowser_NeverSeesYourBasket_AndAForgedOrMalformedCookieSeesNothing()
    {
        var (factory, sales, stock) = Shop();
        var variant = NewVariant(sales, stock);
        using var mine = Guest(factory);
        await Send(mine, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 1 });

        using var other = Guest(factory);
        var otherBody = await BodyAsync(await Send(other, HttpMethod.Get, "/api/cart"));

        using var forger = factory.CreateClient();
        forger.DefaultRequestHeaders.Add("X-Dev-Customer", "none");
        var forged = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
        forged.Headers.Add("Cookie", "romp-guest=" + new string('A', 43));
        var forgedBody = await BodyAsync(await forger.SendAsync(forged));
        var junk = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
        junk.Headers.Add("Cookie", "romp-guest=' OR 1=1 --");
        var junkResponse = await forger.SendAsync(junk);

        Assert.Equal(0, otherBody.GetProperty("itemCount").GetInt32());
        Assert.Equal(0, forgedBody.GetProperty("itemCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, junkResponse.StatusCode);
        Assert.Equal(0, (await BodyAsync(junkResponse)).GetProperty("itemCount").GetInt32());
    }

    [Fact]
    [Trait("Spec", "CRT-9")]
    public async Task TheSignedInCustomer_HasTheirOwnBasket_SeparateFromAnyGuest()
    {
        var (factory, sales, stock) = Shop();
        var variant = NewVariant(sales, stock, 700m);
        using var customer = factory.CreateClient();
        using var guest = Guest(factory);

        var added = await Send(customer, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 3 });
        var customerRead = await BodyAsync(await Send(customer, HttpMethod.Get, "/api/cart"));
        var guestRead = await BodyAsync(await Send(guest, HttpMethod.Get, "/api/cart"));

        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.DoesNotContain(SetCookies(added), c => c.StartsWith("romp-guest=", StringComparison.Ordinal));
        Assert.True(customerRead.GetProperty("itemCount").GetInt32() >= 3);
        Assert.Equal(0, guestRead.GetProperty("itemCount").GetInt32());

        await Send(customer, HttpMethod.Delete, $"/api/cart/lines/{variant}");
    }
}
