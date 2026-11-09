using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Orders.Application.Payments;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Orders.Domain.Payments;
using Romp.Modules.Orders.Infrastructure.Payments;
using Romp.Modules.Orders.Infrastructure.Persistence;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Api.IntegrationTests.Orders;

/// <summary>
/// Online payment through the real HTTP API against a real PostgreSQL (orders payments Y3, Y4, Y9): placing a card order, opening the payment
/// page, the signed callback, and the attacks a hostile caller would try on the public routes. Catalog and Warehouse are in-memory fakes; Cart,
/// Orders and the fake gateway are real. These tests run on CI (Linux): the local Windows policy blocks the host (CLAUDE.md).
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class PaymentEndpointsTests(ProductionApiFixture api) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// These tests place orders against in-memory stock fakes, so the warehouse consumer rightly cannot confirm a reservation that was never made
    /// and the message would retry and dead-letter. That is noise in a host shared with other suites (some assert a healthy outbox), so what this
    /// class leaves unprocessed in the Orders outbox is removed when each test ends.
    /// </summary>
    public async Task DisposeAsync()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdrDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM \"ORDR\".\"OUTB_MSG\" WHERE \"PROC_DTE\" IS NULL");
    }

    private const string Origin = "http://localhost";
    private const string Secret = "integration-test-fake-gateway-secret-0123456789";
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

    private sealed record Shop(WebApplicationFactory<Program> Factory, FakeSales Sales, FakeAvailability Stock, FakeGateway? Gateway);

    /// <summary>A host with (or without) the fake gateway switched on. The settings are read while the host is built, so they are set around that moment only.</summary>
    private Shop NewShop(bool gateway = true)
    {
        var sales = new FakeSales();
        var stock = new FakeAvailability();
        var factory = api.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IVariantSalesQueries>(sales);
            services.AddSingleton<IStockAvailabilityQueries>(stock);
            services.AddSingleton<IStockReservations>(new FakeReservations());
        }));
        var names = new[] { "Orders__GatewayProvider", "Orders__FakeGatewayWebhookSecret", "Orders__EnabledOnlineMethods__0", "Orders__EnabledOnlineMethods__1", "Orders__EnabledOnlineMethods__2" };
        try
        {
            if (gateway)
            {
                Environment.SetEnvironmentVariable(names[0], "fake");
                Environment.SetEnvironmentVariable(names[1], Secret);
                Environment.SetEnvironmentVariable(names[2], "CARD");
                Environment.SetEnvironmentVariable(names[3], "EASY");
                Environment.SetEnvironmentVariable(names[4], "JAZZ");
            }

            _ = factory.Services;
        }
        finally
        {
            foreach (var name in names)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        return new Shop(factory, sales, stock, gateway ? factory.Services.GetRequiredService<FakeGateway>() : null);
    }

    private static long NewVariant(Shop shop, decimal price = 1000m)
    {
        var id = Random.Shared.NextInt64(1_000_000, 900_000_000);
        shop.Sales.Variants[id] = new VariantSale(
            id, id + 1, "Floral Dress", "/girls/dresses/floral-dress", "Red", "2-3Y", $"ROMP-S{id}", id + 2, price, null, true, 3, id + 3, "images/a.jpg", "A red dress", 40, 60);
        shop.Stock.Available[id + 2] = 50;
        return id;
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory, string? customer = "none")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        if (customer is not null)
        {
            client.DefaultRequestHeaders.Add("X-Dev-Customer", customer);
        }

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

    private static async Task<(short ProvinceId, short CityId)> AnyCityAsync(HttpClient client)
    {
        var provinces = await BodyAsync(await client.GetAsync("/api/geography/provinces"));
        var provinceId = provinces[0].GetProperty("id").GetInt16();
        var cities = await BodyAsync(await client.GetAsync($"/api/geography/provinces/{provinceId}/cities"));
        return (provinceId, cities[0].GetProperty("id").GetInt16());
    }

    private static object OrderIn((short ProvinceId, short CityId) place, string method) => new
    {
        contact = new { name = "Ayesha Khan", email = "ayesha@example.com", mobile = "0300 1234567", whatsAppOptIn = true },
        address = new { recipientName = "Ayesha Khan", recipientPhone = "0300 1234567", lineOne = "House 4, Street 2", provinceId = place.ProvinceId, cityId = place.CityId, postcode = "54000" },
        paymentMethod = method,
    };

    /// <summary>A signed-in customer keeps their basket between tests that use other fake shops: start from an empty one.</summary>
    private static async Task ClearBasketAsync(HttpClient client)
    {
        var cart = await BodyAsync(await client.GetAsync("/api/cart"));
        if (!cart.TryGetProperty("lines", out var lines))
        {
            return;
        }

        foreach (var line in lines.EnumerateArray().Select(l => l.GetProperty("variantId").GetInt64()).ToList())
        {
            await Send(client, HttpMethod.Delete, $"/api/cart/lines/{line}");
        }
    }

    /// <summary>Places an order for one 1,000-rupee dress and returns its number and, for an online method, its payment token.</summary>
    private static async Task<(string Number, string? PayToken, decimal Total)> PlaceAsync(Shop shop, HttpClient client, string method)
    {
        await ClearBasketAsync(client);
        var variant = NewVariant(shop);
        await Send(client, HttpMethod.Post, "/api/cart/lines", new { variantId = variant, quantity = 1 });
        var response = await Send(client, HttpMethod.Post, "/api/orders", OrderIn(await AnyCityAsync(client), method), Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await BodyAsync(response);
        var token = body.TryGetProperty("payToken", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        return (body.GetProperty("orderNumber").GetString()!, token, 0m);
    }

    private static async Task<string> StartAsync(HttpClient client, string token)
    {
        var started = await Send(client, HttpMethod.Post, "/api/orders/payments/start", new { token });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        return (await BodyAsync(started)).GetProperty("redirectUrl").GetString()!;
    }

    private static Task<HttpResponseMessage> Webhook(HttpClient client, byte[] body, Dictionary<string, string> headers, string provider = "fake")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/payments/webhook/{provider}") { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new("application/json");
        foreach (var header in headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return client.SendAsync(request);
    }

    private static async Task<(string Status, string OrderNumber)> StatusAsync(HttpClient client, string token)
    {
        var response = await client.GetAsync($"/api/orders/payments/status?token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        return (body.GetProperty("state").GetString()!, body.GetProperty("orderNumber").GetString()!);
    }

    private static async Task<short> OrderStatusAsync(Shop shop, string number)
    {
        using var scope = shop.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdrDbContext>();
        return await db.Orders.AsNoTracking().Where(o => o.OrderNumber == number).Select(o => o.StatusId).SingleAsync();
    }

    // ---- the journey -------------------------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-1")]
    [Trait("Spec", "TC-CHK-11")]
    public async Task AGuest_PaysByCard_ThroughTheHostedPageAndTheSignedCallback_AndTheOrderEndsPaid()
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);
        var (number, token, _) = await PlaceAsync(shop, client, "CARD");
        Assert.NotNull(token);

        var redirect = await StartAsync(client, token!);
        Assert.StartsWith("/customer-api/orders/payments/fake/fk_", redirect, StringComparison.Ordinal);
        Assert.Equal(("confirming", number), await StatusAsync(client, token!));

        var reference = shop.Gateway!.Sessions.Single(s => s.OrderNumber == number).GatewayRef;
        var (body, headers) = shop.Gateway.BuildCallback(reference, GatewayOutcome.Paid, eventId: "it-evt-" + Guid.NewGuid().ToString("N"));
        var delivered = await Webhook(client, body, headers);
        var repeat = await Webhook(client, body, headers);

        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(("paid", number), await StatusAsync(client, token!));
        Assert.Equal(OrderStatuses.Paid, await OrderStatusAsync(shop, number));
    }

    [Fact]
    [Trait("Spec", "PAY-2")]
    public async Task ADeclinedCard_ShowsFailed_AndTheShopperCanTryAgain()
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);
        var (number, token, _) = await PlaceAsync(shop, client, "CARD");
        await StartAsync(client, token!);
        var reference = shop.Gateway!.Sessions.Single(s => s.OrderNumber == number).GatewayRef;
        var (body, headers) = shop.Gateway.BuildCallback(reference, GatewayOutcome.Failed, amount: 0m);

        Assert.Equal(HttpStatusCode.OK, (await Webhook(client, body, headers)).StatusCode);

        Assert.Equal(("failed", number), await StatusAsync(client, token!));
        Assert.Equal(OrderStatuses.PaymentPending, await OrderStatusAsync(shop, number));
        var again = await StartAsync(client, token!);
        Assert.NotNull(again);
        Assert.Equal(2, shop.Gateway.Sessions.Count(s => s.OrderNumber == number));
    }

    [Fact]
    [Trait("Spec", "PAY-10")]
    public async Task TheDemoPaymentPage_TakesThePayment_AndSendsTheShopperBack_WhoStillSeesOnlyTheStoredState()
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);
        var (number, token, _) = await PlaceAsync(shop, client, "CARD");
        var redirect = await StartAsync(client, token!);
        var page = await client.GetAsync(redirect.Replace("/customer-api/", "/api/", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("noindex", await page.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var done = await client.PostAsync(redirect.Replace("/customer-api/", "/api/", StringComparison.Ordinal) + "/complete", new FormUrlEncodedContent([new("outcome", "paid")]));

        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        Assert.Contains("/checkout/payment-return?token=", done.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(("paid", number), await StatusAsync(client, token!));
    }

    [Fact]
    [Trait("Spec", "PAY-6")]
    public async Task APaymentWhoseCallbackIsLost_StaysConfirming_UntilReconciliationFindsIt()
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);
        var (number, token, _) = await PlaceAsync(shop, client, "CARD");
        var redirect = await StartAsync(client, token!);

        await client.PostAsync(redirect.Replace("/customer-api/", "/api/", StringComparison.Ordinal) + "/complete", new FormUrlEncodedContent([new("outcome", "paid-lost")]));

        Assert.Equal(("confirming", number), await StatusAsync(client, token!));
        Assert.Equal(OrderStatuses.PaymentPending, await OrderStatusAsync(shop, number));
    }

    // ---- what is offered ----------------------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-14")]
    public async Task TheMethodsOffered_AreCashOnDeliveryOnly_UntilAGatewayIsSwitchedOn()
    {
        var off = NewShop(gateway: false);
        var on = NewShop();

        var offMethods = await BodyAsync(await off.Factory.CreateClient().GetAsync("/api/orders/payment-methods"));
        var onMethods = await BodyAsync(await on.Factory.CreateClient().GetAsync("/api/orders/payment-methods"));

        Assert.Equal(["COD"], offMethods.EnumerateArray().Select(m => m.GetProperty("code").GetString()));
        Assert.Equal(["COD", "EASY", "JAZZ", "CARD"], onMethods.EnumerateArray().Select(m => m.GetProperty("code").GetString()).OrderBy(c => c == "COD" ? 0 : c == "EASY" ? 1 : c == "JAZZ" ? 2 : 3));
    }

    [Fact]
    [Trait("Spec", "PAY-7")]
    public async Task WithNoGatewayOn_ACardOrderIsRefused_AndWhileTheGatewayIsDownOnlyCashOnDeliveryIsOffered()
    {
        var off = NewShop(gateway: false);
        using var offClient = Client(off.Factory);
        await Send(offClient, HttpMethod.Post, "/api/cart/lines", new { variantId = NewVariant(off), quantity = 1 });
        var refused = await Send(offClient, HttpMethod.Post, "/api/orders", OrderIn(await AnyCityAsync(offClient), "CARD"), Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        var on = NewShop();
        var circuit = on.Factory.Services.GetRequiredService<ResilientPaymentGateway>();
        Assert.True(circuit.IsAvailable);
        on.Gateway!.Mode = FakeGatewayMode.Down;
        using var client = Client(on.Factory);
        var (_, token, _) = await PlaceAsync(on, client, "CARD");
        for (var i = 0; i < 6; i++)
        {
            await Send(client, HttpMethod.Post, "/api/orders/payments/start", new { token });
        }

        var methods = await BodyAsync(await client.GetAsync("/api/orders/payment-methods"));
        Assert.Equal(["COD"], methods.EnumerateArray().Select(m => m.GetProperty("code").GetString()));
        var summary = await BodyAsync(await client.GetAsync("/api/orders/checkout?method=cod"));
        Assert.True(summary.GetProperty("onlinePaymentsPaused").GetBoolean());
        on.Gateway.Mode = FakeGatewayMode.Normal;
    }

    // ---- attacks on the webhook ----------------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "PAY-4")]
    [Trait("Spec", "SEC-10")]
    public async Task ForgedTamperedUnsignedAndReplayedCallbacks_AreAllRefused_AndNothingChanges()
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);
        var (number, token, _) = await PlaceAsync(shop, client, "CARD");
        await StartAsync(client, token!);
        var reference = shop.Gateway!.Sessions.Single(s => s.OrderNumber == number).GatewayRef;
        var (body, headers) = shop.Gateway.BuildCallback(reference, GatewayOutcome.Paid);

        // no signature at all
        Assert.Equal(HttpStatusCode.Unauthorized, (await Webhook(client, body, new())).StatusCode);

        // a guessed signature
        Assert.Equal(HttpStatusCode.Unauthorized, (await Webhook(client, body, new() { [FakeGateway.SignatureHeader] = $"t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()},v1={new string('0', 64)}" })).StatusCode);

        // a good signature over a different body (the body tampered with after signing)
        var tampered = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(body).Replace("paid", "PAID", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Webhook(client, tampered, headers)).StatusCode);

        // a genuine, correctly signed message from ten minutes ago, replayed
        var (oldBody, oldHeaders) = shop.Gateway.BuildCallback(reference, GatewayOutcome.Paid, at: DateTimeOffset.UtcNow.AddMinutes(-10));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Webhook(client, oldBody, oldHeaders)).StatusCode);

        // signed garbage
        var (_, garbageHeaders) = shop.Gateway.BuildCallback(reference, GatewayOutcome.Paid);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Webhook(client, Encoding.UTF8.GetBytes("not json"), garbageHeaders)).StatusCode);

        Assert.Equal(OrderStatuses.PaymentPending, await OrderStatusAsync(shop, number));
        Assert.Equal(("confirming", number), await StatusAsync(client, token!));
    }

    [Fact]
    [Trait("Spec", "SEC-26")]
    [Trait("Spec", "NFR-04")]
    public async Task AFloodOfForgedCallbacks_FillsNeitherTheLogTableNorTheAuditOutbox()
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);
        var (body, _) = shop.Gateway!.BuildCallback("fk_flood", GatewayOutcome.Paid);
        var forged = new Dictionary<string, string> { [FakeGateway.SignatureHeader] = $"t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()},v1={new string('e', 64)}" };
        using var scope = shop.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdrDbContext>();
        var storedBefore = await db.PaymentWebhooks.CountAsync();
        var auditBefore = (await db.Set<Romp.BuildingBlocks.Persistence.Outbox.OutboxMessage>().Where(m => m.EventType == "AuditRecorded").Select(m => m.Payload).ToListAsync()).Count(p => p.Contains("PAYM_WBHK", StringComparison.Ordinal));

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 150; i++)
        {
            statuses.Add((await Webhook(client, body, forged)).StatusCode);
        }

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.Unauthorized, s));
        var stored = await db.PaymentWebhooks.CountAsync() - storedBefore;
        var audit = (await db.Set<Romp.BuildingBlocks.Persistence.Outbox.OutboxMessage>().Where(m => m.EventType == "AuditRecorded").Select(m => m.Payload).ToListAsync()).Count(p => p.Contains("PAYM_WBHK", StringComparison.Ordinal));
        Assert.InRange(stored, 1, 60);
        Assert.Equal(auditBefore, audit);
    }

    [Fact]
    [Trait("Spec", "SEC-10")]
    public async Task TheWebhookRoute_RefusesTheWrongProvider_TheWrongVerb_AndAnOversizedBody()
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);
        var (body, headers) = shop.Gateway!.BuildCallback("fk_nothing", GatewayOutcome.Paid);

        Assert.Equal(HttpStatusCode.NotFound, (await Webhook(client, body, headers, provider: "stripe")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Webhook(client, body, headers, provider: "..%2F..%2Fadmin")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/api/orders/payments/webhook/fake")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PutAsync("/api/orders/payments/webhook/fake", new ByteArrayContent(body))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Webhook(client, new byte[70 * 1024], headers)).StatusCode);
    }

    [Fact]
    [Trait("Spec", "SEC-10")]
    public async Task TheWebhook_NeedsNoSignIn_ButAGuestCookieOrAnAdminHeaderGainsNothing()
    {
        var shop = NewShop();
        using var client = Client(shop.Factory, customer: "other");
        var (number, token, _) = await PlaceAsync(shop, Client(shop.Factory), "CARD");
        await StartAsync(client, token!);
        var (body, _) = shop.Gateway!.BuildCallback(shop.Gateway.Sessions.Single(s => s.OrderNumber == number).GatewayRef, GatewayOutcome.Paid);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders/payments/webhook/fake") { Content = new ByteArrayContent(body) };
        request.Headers.Add("X-Dev-Customer", "1");
        request.Headers.Add("Authorization", "Bearer admin");
        request.Headers.Add("X-Forwarded-For", "127.0.0.1");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(OrderStatuses.PaymentPending, await OrderStatusAsync(shop, number));
    }

    // ---- attacks on starting and following a payment -------------------------------------------------------------------------------------------

    [Theory]
    [Trait("Spec", "SEC-05")]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("' OR 1=1 --")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("../../../etc/passwd")]
    [InlineData("%00")]
    public async Task ABadTokenToStartOrFollow_IsJustNotFound_NeverAnError_NeverADetail(string token)
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);

        var start = await Send(client, HttpMethod.Post, "/api/orders/payments/start", new { token });
        var status = await client.GetAsync($"/api/orders/payments/status?token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.DoesNotContain("exception", (await start.Content.ReadAsStringAsync()).ToLowerInvariant(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public async Task ATokenTamperedWith_OrBorrowedFromACashOnDeliveryOrder_StartsNothing()
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);
        var (_, token, _) = await PlaceAsync(shop, client, "CARD");
        var (_, cod, _) = await PlaceAsync(shop, client, "COD");
        Assert.Null(cod);
        var broken = token![..^2] + (token[^2] == 'A' ? "B" : "A") + token[^1];

        var tampered = await Send(client, HttpMethod.Post, "/api/orders/payments/start", new { token = broken });

        Assert.Equal(HttpStatusCode.NotFound, tampered.StatusCode);
        Assert.Empty(shop.Gateway!.Sessions);
    }

    [Fact]
    [Trait("Spec", "SEC-12")]
    public async Task AnotherSite_CannotStartAPayment_ButTheGatewayServerToServerRouteDoesNotNeedAnOrigin()
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);
        var (_, token, _) = await PlaceAsync(shop, client, "CARD");

        var foreign = await Send(client, HttpMethod.Post, "/api/orders/payments/start", new { token }, origin: "https://evil.example");

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Empty(shop.Gateway!.Sessions);
    }

    [Fact]
    [Trait("Spec", "SEC-25")]
    public async Task PaymentResponses_AreNeverCached_AndShowNoPersonalDetails()
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);
        var (number, token, _) = await PlaceAsync(shop, client, "CARD");

        var started = await Send(client, HttpMethod.Post, "/api/orders/payments/start", new { token });
        var status = await client.GetAsync($"/api/orders/payments/status?token={Uri.EscapeDataString(token!)}");

        Assert.Contains("no-store", started.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Contains("no-store", status.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        var text = await status.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ayesha", text.ToLowerInvariant(), StringComparison.Ordinal);
        Assert.DoesNotContain("1234567", text, StringComparison.Ordinal);
        Assert.Contains(number, text, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Spec", "SEC-10")]
    [InlineData("fk_zzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("../../appsettings")]
    [InlineData("fk_0000000000000000000000000' OR '1'='1")]
    public async Task TheDemoPage_ServesOnlyRealSessions_AndRefusesEverythingElse(string reference)
    {
        var shop = NewShop();
        using var client = Client(shop.Factory);

        var page = await client.GetAsync($"/api/orders/payments/fake/{Uri.EscapeDataString(reference)}");
        var complete = await client.PostAsync($"/api/orders/payments/fake/{Uri.EscapeDataString(reference)}/complete", new FormUrlEncodedContent([new("outcome", "paid")]));

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, complete.StatusCode);
    }

    [Fact]
    [Trait("Spec", "PAY-15")]
    public async Task WithNoGateway_TheDemoPageDoesNotExistAtAll()
    {
        var shop = NewShop(gateway: false);
        using var client = Client(shop.Factory);

        var page = await client.GetAsync("/api/orders/payments/fake/fk_000000000000000000000000");

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        var (body, headers) = (Encoding.UTF8.GetBytes("{}"), new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.NotFound, (await Webhook(client, body, headers)).StatusCode);
    }

    // ---- a customer's own orders ------------------------------------------------------------------------------------------------------------

    [Fact]
    [Trait("Spec", "ORD-13")]
    [Trait("Spec", "SEC-05")]
    public async Task ACustomerReadsTheirOwnOrders_AnotherCustomerAndAGuestSeeNothingOfThem()
    {
        var shop = NewShop(gateway: false);
        using var owner = Client(shop.Factory, customer: null);
        using var other = Client(shop.Factory, customer: "other");
        using var guest = Client(shop.Factory, customer: "none");
        var (number, _, _) = await PlaceAsync(shop, owner, "COD");

        var mine = await owner.GetAsync($"/api/orders/mine/{number}");
        var theirs = await other.GetAsync($"/api/orders/mine/{number}");
        var anon = await guest.GetAsync($"/api/orders/mine/{number}");
        var list = await BodyAsync(await owner.GetAsync("/api/orders/mine?page=1&pageSize=5"));
        var otherList = await BodyAsync(await other.GetAsync("/api/orders/mine"));

        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        Assert.True(anon.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound or HttpStatusCode.Forbidden, anon.StatusCode.ToString());
        Assert.Contains(list.GetProperty("items").EnumerateArray(), i => i.GetProperty("orderNumber").GetString() == number);
        Assert.DoesNotContain(otherList.GetProperty("items").EnumerateArray(), i => i.GetProperty("orderNumber").GetString() == number);
        Assert.Contains("no-store", mine.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "ORD-14")]
    public async Task TheOrderList_IsPaged_AndAHugePageSizeIsClamped()
    {
        var shop = NewShop(gateway: false);
        using var owner = Client(shop.Factory, customer: null);
        for (var i = 0; i < 3; i++)
        {
            await PlaceAsync(shop, owner, "COD");
        }

        var page = await BodyAsync(await owner.GetAsync("/api/orders/mine?page=1&pageSize=100000"));

        Assert.Equal(100, page.GetProperty("pageSize").GetInt32());
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 3);
    }
}
