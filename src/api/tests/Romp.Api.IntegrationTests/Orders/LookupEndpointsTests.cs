using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.BuildingBlocks.Web.Captcha;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Orders.Application.OrderLookup;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Api.IntegrationTests.Orders;

/// <summary>
/// Guest order look-up through the real HTTP API against a real PostgreSQL (orders L4, FR-37, SEC-06, SEC-07): one generic mismatch,
/// the CAPTCHA after failures, the rate limit, and the signed link. Each test names its own caller address so the throttle of one test
/// never leaks into another.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class LookupEndpointsTests(ProductionApiFixture api)
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

    private sealed class FakeReservations : IStockReservations
    {
        public Task<ReservationResult> ReserveAsync(string placementKey, IReadOnlyList<ReservationLineRequest> lines, CancellationToken cancellationToken) =>
            Task.FromResult(new ReservationResult(ReservationOutcome.Reserved, 1, []));

        public Task ReleaseAsync(string placementKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Takes the caller address from <c>X-Test-Ip</c>, as a trusted proxy would, so a test controls which address the throttle sees.</summary>
    private sealed class TestIpFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Ip", out var ip) && System.Net.IPAddress.TryParse(ip, out var parsed))
                {
                    context.Connection.RemoteIpAddress = parsed;
                }

                await continuation();
            });
            next(app);
        };
    }

    private WebApplicationFactory<Program> Shop(out long variant)
    {
        var sales = new FakeSales();
        var stock = new FakeAvailability();
        var id = Random.Shared.NextInt64(1_000_000, 900_000_000);
        sales.Variants[id] = new VariantSale(
            id, id + 1, "Floral Dress", "/girls/dresses/floral-dress", "Red", "2-3Y", $"ROMP-S{id}", id + 2, 1250m, null, true, 3, id + 3, "images/a.jpg", "A red dress", 40, 60);
        stock.Available[id + 2] = 10;
        variant = id;
        return api.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IVariantSalesQueries>(sales);
            services.AddSingleton<IStockAvailabilityQueries>(stock);
            services.AddSingleton<IStockReservations>(new FakeReservations());
            services.AddSingleton<IStartupFilter, TestIpFilter>();
        }));
    }

    private static HttpClient Guest(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Dev-Customer", "none");
        return client;
    }

    private static string NewIp() => $"10.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}";

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body = null, string? ip = null, string? key = null, string? origin = Origin)
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

        if (ip is not null)
        {
            request.Headers.Add("X-Test-Ip", ip);
        }

        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return client.SendAsync(request);
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    /// <summary>A guest places a cash on delivery order; returns its number and the mobile typed at checkout.</summary>
    private static async Task<(string Number, string Mobile)> PlaceAsync(HttpClient client, long variant)
    {
        await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 2 });
        var provinces = await BodyAsync(await client.GetAsync("/api/geography/provinces"));
        var provinceId = provinces[0].GetProperty("id").GetInt16();
        var cities = await BodyAsync(await client.GetAsync($"/api/geography/provinces/{provinceId}/cities"));
        var cityId = cities[0].GetProperty("id").GetInt16();
        var mobile = $"0300{Random.Shared.Next(1_000_000, 9_999_999)}";
        var placed = await Send(client, HttpMethod.Post, "/api/orders", new
        {
            contact = new { name = "Ayesha Khan", email = "ayesha@example.com", mobile, whatsAppOptIn = false },
            address = new { recipientName = "Ayesha Khan", recipientPhone = mobile, lineOne = "House 4, Street 2", provinceId, cityId, postcode = "54000" },
            paymentMethod = "COD",
        }, key: Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        return ((await BodyAsync(placed)).GetProperty("orderNumber").GetString()!, mobile);
    }

    [Fact]
    [Trait("Spec", "OLK-1")]
    [Trait("Spec", "OLK-6")]
    public async Task ACorrectNumberAndMobile_ShowsTheOrder_WithNoAddressAndNoFullPhone()
    {
        var factory = Shop(out var variant);
        using var client = Guest(factory);
        var (number, mobile) = await PlaceAsync(client, variant);

        var response = await Send(client, HttpMethod.Post, "/api/orders/lookup", new { orderNumber = number.ToLowerInvariant(), mobile = $"+92 {mobile[1..4]} {mobile[4..]}" }, NewIp());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        var text = await response.Content.ReadAsStringAsync();
        var order = JsonSerializer.Deserialize<JsonElement>(text, Json);
        Assert.Equal(number, order.GetProperty("orderNumber").GetString());
        Assert.Equal(2500m, order.GetProperty("subtotal").GetDecimal());
        Assert.DoesNotContain("House 4", text, StringComparison.Ordinal);
        Assert.DoesNotContain(mobile[1..], text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "OLK-2")]
    [Trait("Spec", "SEC-07")]
    public async Task AWrongMobile_AndAnUnknownNumber_GetTheSameStatusAndBody()
    {
        var factory = Shop(out var variant);
        using var client = Guest(factory);
        var (number, mobile) = await PlaceAsync(client, variant);

        var wrongMobile = await Send(client, HttpMethod.Post, "/api/orders/lookup", new { orderNumber = number, mobile = "03009999999" }, NewIp());
        var unknown = await Send(client, HttpMethod.Post, "/api/orders/lookup", new { orderNumber = "RMP-2026-99999-0", mobile }, NewIp());

        Assert.Equal(HttpStatusCode.NotFound, wrongMobile.StatusCode);
        Assert.Equal(wrongMobile.StatusCode, unknown.StatusCode);
        Assert.Equal(await Normalised(wrongMobile), await Normalised(unknown));
    }

    // The problem document carries a trace id that differs per request; everything else must be identical.
    private static async Task<string> Normalised(HttpResponseMessage response)
    {
        var body = await BodyAsync(response);
        return string.Join('|', body.EnumerateObject().Where(p => !p.NameEquals("traceId")).OrderBy(p => p.Name).Select(p => $"{p.Name}={p.Value}"));
    }

    [Fact]
    [Trait("Spec", "OLK-4")]
    public async Task AfterThreeFailures_TheCaptchaIsRequired_AndPassingItShowsTheOrder()
    {
        var factory = Shop(out var variant);
        using var client = Guest(factory);
        var (number, mobile) = await PlaceAsync(client, variant);
        var ip = NewIp();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await Send(client, HttpMethod.Post, "/api/orders/lookup", new { orderNumber = number, mobile = $"0300111111{i}" }, ip)).StatusCode);
        }

        var without = await Send(client, HttpMethod.Post, "/api/orders/lookup", new { orderNumber = number, mobile }, ip);
        var with = await Send(client, HttpMethod.Post, "/api/orders/lookup", new { orderNumber = number, mobile, captchaToken = FakeCaptchaVerifier.PassToken }, ip);

        Assert.Equal(HttpStatusCode.PreconditionRequired, without.StatusCode);
        Assert.True((await BodyAsync(without)).GetProperty("captchaRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, with.StatusCode);
    }

    [Fact]
    [Trait("Spec", "OLK-3")]
    public async Task AnAddress_IsRateLimited_AfterTwentyAttempts_AndAnotherAddressIsNot()
    {
        var factory = Shop(out var variant);
        using var client = Guest(factory);
        var (number, mobile) = await PlaceAsync(client, variant);
        var ip = NewIp();
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, "/api/orders/lookup", new { orderNumber = number, mobile }, ip)).StatusCode);
        }

        var cut = await Send(client, HttpMethod.Post, "/api/orders/lookup", new { orderNumber = number, mobile }, ip);
        var other = await Send(client, HttpMethod.Post, "/api/orders/lookup", new { orderNumber = number, mobile }, NewIp());

        Assert.Equal(HttpStatusCode.TooManyRequests, cut.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    [Trait("Spec", "OLK-2")]
    public async Task APostFromAnotherSite_IsRefused()
    {
        var factory = Shop(out var variant);
        using var client = Guest(factory);
        var (number, mobile) = await PlaceAsync(client, variant);

        var response = await Send(client, HttpMethod.Post, "/api/orders/lookup", new { orderNumber = number, mobile }, NewIp(), origin: "https://evil.example");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "OLK-5")]
    public async Task TheSignedLink_ShowsTheOrder_AndABrokenOrForeignTokenDoesNot()
    {
        var factory = Shop(out var variant);
        using var client = Guest(factory);
        var (number, _) = await PlaceAsync(client, variant);
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<ILookupLinks>().Create(number);

        var opened = await Send(client, HttpMethod.Get, $"/api/orders/lookup/link?token={Uri.EscapeDataString(token)}", ip: NewIp());
        var tampered = await Send(client, HttpMethod.Get, $"/api/orders/lookup/link?token={Uri.EscapeDataString(token + "x")}", ip: NewIp());
        var none = await Send(client, HttpMethod.Get, "/api/orders/lookup/link", ip: NewIp());

        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal(number, (await BodyAsync(opened)).GetProperty("orderNumber").GetString());
        Assert.Equal(HttpStatusCode.NotFound, tampered.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
    }
}
