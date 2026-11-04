using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Customers;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Api.IntegrationTests.Cart;

/// <summary>
/// A guest fills a basket, then registers or signs in: the basket comes with them and the guest cookie is cleared (cart C8, CRT-3, FR-35),
/// through the real HTTP API with real customer sign-in. Catalog and Warehouse are in-memory fakes.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class CartMergeOnSignInTests(ProductionApiFixture api)
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

        public Task<IReadOnlyList<StockAvailability>> ListAvailabilityAsync(long afterVariantId, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<StockAvailability>>([]);

        public Task<StockAvailability?> GetAsync(long variantId, CancellationToken cancellationToken) => Task.FromResult(Available.TryGetValue(variantId, out var available) ? new StockAvailability(variantId, $"SKU-{variantId}", available, 1) : null);
    }

    private WebApplicationFactory<Program> RealSignIn(FakeSales sales, FakeAvailability stock) => api.Factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Customers:Auth:UseRealSignIn", "true");
        builder.UseSetting("Customers:Auth:AllowInsecureCookies", "true");
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, CustomerAuthEndpointsTests.TestClientIpStartupFilter>());
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IVariantSalesQueries>(sales);
            services.AddSingleton<IStockAvailabilityQueries>(stock);
        });
    });

    private static long NewVariant(FakeSales sales, FakeAvailability stock, decimal price)
    {
        var id = Random.Shared.NextInt64(1_000_000, 900_000_000);
        sales.Variants[id] = new VariantSale(
            id, id + 1, "Floral Dress", "/girls/dresses/floral-dress", "Red", "2-3Y", $"ROMP-S{id}", id + 2, price, null, true, 3, id + 3, "images/a.jpg", "A red dress", 40, 60);
        stock.Available[id + 2] = 10;
        return id;
    }

    private static HttpClient Browser(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add(CustomerAuthEndpointsTests.TestClientIpStartupFilter.Header, $"198.19.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}");
        return client;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body = null)
    {
        // The customer routes expect the storefront address as the origin; the basket routes fall back to the request host here.
        var origin = path.StartsWith("/api/customers/", StringComparison.Ordinal) ? "http://localhost:4000" : Origin;
        return SendFrom(client, method, path, body, origin);
    }

    private static Task<HttpResponseMessage> SendFrom(HttpClient client, HttpMethod method, string path, object? body, string origin)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        request.Headers.Add("Origin", origin);
        return client.SendAsync(request);
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    private static object Registration(string email) =>
        new { email, firstName = "Ayesha", lastName = "Khan", mobile = "0300 1234567", password = "Strong1Pass", keepSignedIn = false };

    [Fact]
    [Trait("Spec", "CRT-3")]
    public async Task ARegisteringGuest_KeepsTheirBasket_AndTheGuestCookieIsCleared()
    {
        var sales = new FakeSales();
        var stock = new FakeAvailability();
        var variant = NewVariant(sales, stock, 900m);
        using var factory = RealSignIn(sales, stock);
        using var browser = Browser(factory);
        await Send(browser, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 3 });

        var registered = await Send(browser, HttpMethod.Post, "/api/customers/auth/register", Registration($"cart.{Guid.NewGuid():N}@example.test"));
        var basket = await BodyAsync(await Send(browser, HttpMethod.Get, "/api/cart"));

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var cookies = registered.Headers.GetValues("Set-Cookie").ToList();
        var guestCookie = Assert.Single(cookies, c => c.StartsWith("romp-guest=", StringComparison.Ordinal));
        Assert.Contains("expires=Thu, 01 Jan 1970", guestCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, basket.GetProperty("itemCount").GetInt32());
        Assert.Equal(2700m, basket.GetProperty("subtotal").GetDecimal());
    }

    [Fact]
    [Trait("Spec", "CRT-3")]
    public async Task SigningInAgain_MergesANewGuestBasketIntoTheAccountBasket_WithoutLossOrDuplication()
    {
        var sales = new FakeSales();
        var stock = new FakeAvailability();
        var shared = NewVariant(sales, stock, 500m);
        var onlyGuest = NewVariant(sales, stock, 300m);
        using var factory = RealSignIn(sales, stock);
        var email = $"cart.{Guid.NewGuid():N}@example.test";

        using (var first = Browser(factory))
        {
            await Send(first, HttpMethod.Post, "/api/customers/auth/register", Registration(email));
            await Send(first, HttpMethod.Post, "/api/cart/lines", new { variantId = shared, quantity = 2 });
        }

        using var second = Browser(factory);
        await Send(second, HttpMethod.Post, "/api/cart/lines", new { variantId = shared, quantity = 1 });
        await Send(second, HttpMethod.Post, "/api/cart/lines", new { variantId = onlyGuest, quantity = 4 });
        var signedIn = await Send(second, HttpMethod.Post, "/api/customers/auth/sign-in", new { email, password = "Strong1Pass", keepSignedIn = false });
        var basket = await BodyAsync(await Send(second, HttpMethod.Get, "/api/cart"));

        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(7, basket.GetProperty("itemCount").GetInt32());
        var lines = basket.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("variantId").GetInt64(), l => l.GetProperty("quantity").GetInt32());
        Assert.Equal(3, lines[shared]);
        Assert.Equal(4, lines[onlyGuest]);
        Assert.Equal(2, lines.Count);
    }
}
