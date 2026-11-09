using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Fulfilment.Domain.Allocations;
using Romp.Modules.Fulfilment.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Fulfilment;

/// <summary>
/// The pick and pack routes through the real HTTP API against PostgreSQL (S14 F12, T1, T3). The development host signs the caller in with every
/// permission; who may call what is proved for every role by the authorization matrix and role behaviour tests, which include these routes.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class FulfilmentEndpointsTests(ProductionApiFixture api)
{
    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    private static long _seq = 4_000_000;

    private HttpClient Client() => api.Factory.CreateClient();

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    /// <summary>Saves a queued fulfilment order with one line of <paramref name="quantity"/> units in bin A-01-1, as allocation would have left it.</summary>
    private async Task<(long Id, string Number, string Gtin)> SeedAsync(int quantity = 2)
    {
        var n = Interlocked.Increment(ref _seq);
        var gtin = "2" + n.ToString("00000000000", System.Globalization.CultureInfo.InvariantCulture) + "0";
        var order = FulfilmentOrder.Create(n, $"RMP-IT{n}", $"it-key-{n}", true, 1500m, DateTimeOffset.UtcNow,
            [new FulfilmentLineSeed(n, $"SKU-{n}", "Floral Dress", "Red", "2-3Y", quantity, 1500m)]);
        order.ApplyAllocation([new AllocationSeed(n, n * 10, "A-01-1", "A  -001-1 ", quantity, gtin, $"SKU-{n}")]);
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FulfDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return (order.Id, order.OrderNumber, gtin);
    }

    private static Task<HttpResponseMessage> ScanAsync(HttpClient client, long id, string? code) =>
        client.PostAsJsonAsync($"/api/fulfilment/orders/{id}/pick/scan", new { code }, Json);

    [Fact]
    [Trait("Spec", "PCK-1")]
    [Trait("Spec", "APP-8")]
    public async Task TheQueueAndThePickList_AnswerPaged_WithTheOrderNumberOnly()
    {
        using var client = Client();
        var (id, number, _) = await SeedAsync();

        var queue = await BodyAsync(await client.GetAsync("/api/fulfilment/queue?stage=pick&pageSize=100"));
        var list = await client.GetAsync($"/api/fulfilment/orders/{id}");
        var text = await list.Content.ReadAsStringAsync();

        Assert.Contains(queue.GetProperty("items").EnumerateArray(), i => i.GetProperty("orderNumber").GetString() == number);
        Assert.True(queue.GetProperty("totalCount").GetInt32() >= 1);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains(number, text);
        Assert.DoesNotContain("customer", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mobile", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/fulfilment/orders/987654321")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "PCK-2")]
    [Trait("Spec", "PCK-10")]
    public async Task ClaimingThenScanning_CountsTheUnit_AndTheStaffMemberComesFromTheSessionNotTheBody()
    {
        using var client = Client();
        var (id, number, gtin) = await SeedAsync(2);

        var claim = await client.PostAsync($"/api/fulfilment/orders/{id}/pick/claim", null);
        var bin = await BodyAsync(await client.PostAsJsonAsync($"/api/fulfilment/orders/{id}/pick/scan", new { code = "A-01-1", staff = "Intruder" }, Json));
        var unit = await BodyAsync(await ScanAsync(client, id, gtin));

        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        Assert.True(bin.GetProperty("accepted").GetBoolean());
        Assert.True(unit.GetProperty("accepted").GetBoolean());
        Assert.Equal(1, unit.GetProperty("pickedUnits").GetInt32());
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FulfDbContext>();
        var staff = await db.Scans.Where(s => s.Code == gtin).Select(s => s.Staff).SingleAsync();
        Assert.NotEqual("Intruder", staff);
        Assert.False(string.IsNullOrWhiteSpace(staff));
    }

    [Fact]
    [Trait("Spec", "PCK-3")]
    public async Task ARefusedScan_IsA200WithTheReasonInWords_NotAnError()
    {
        using var client = Client();
        var (id, _, _) = await SeedAsync();
        await client.PostAsync($"/api/fulfilment/orders/{id}/pick/claim", null);
        await ScanAsync(client, id, "A-01-1");

        var response = await ScanAsync(client, id, "2999999999990");
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.GetProperty("accepted").GetBoolean());
        Assert.Equal("GTIN_NOT_ON_ORDER", body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
    }

    [Theory]
    [Trait("Spec", "SEC-25")]
    [InlineData("")]
    [InlineData("A-01-1'; DROP TABLE \"FULF\".\"FULF_ORDR\";--")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task HostileScanInput_AnswersABadRequest_AndTheTablesSurvive(string code)
    {
        using var client = Client();
        var (id, _, _) = await SeedAsync();
        await client.PostAsync($"/api/fulfilment/orders/{id}/pick/claim", null);

        var response = await ScanAsync(client, id, code);

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict, $"was {(int)response.StatusCode}");
        using var scope = api.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<FulfDbContext>().Orders.AnyAsync(o => o.Id == id));
    }

    [Fact]
    [Trait("Spec", "ADR-0011")]
    public async Task AnOversizedBody_IsRefusedBeforeItIsRead()
    {
        using var client = Client();
        var (id, _, _) = await SeedAsync();
        using var content = new StringContent("{\"code\":\"" + new string('A', 50_000) + "\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"/api/fulfilment/orders/{id}/pick/scan", content);

        Assert.True(response.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest or HttpStatusCode.Conflict, $"was {(int)response.StatusCode}");
    }

    [Fact]
    [Trait("Spec", "PEX-4")]
    public async Task TheExceptionQueue_AnswersPaged_AndAnUnknownExceptionIsNotFound()
    {
        using var client = Client();

        var list = await client.GetAsync("/api/fulfilment/exceptions?pageSize=5");
        var resolve = await client.PostAsJsonAsync("/api/fulfilment/exceptions/987654321/resolve", new { action = "restocked" }, Json);
        var bad = await client.PostAsJsonAsync("/api/fulfilment/exceptions/987654321/resolve", new { action = "delete-everything" }, Json);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(5, (await BodyAsync(list)).GetProperty("pageSize").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, resolve.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    [Trait("Spec", "PAK-10")]
    public async Task OpeningAnOrderThatIsNotPicked_AnswersABadRequest_AndASlipIsNotAvailableYet()
    {
        using var client = Client();
        var (id, _, _) = await SeedAsync();

        var open = await client.PostAsync($"/api/fulfilment/orders/{id}/pack/open", null);
        var slip = await client.GetAsync($"/api/fulfilment/orders/{id}/slip");

        Assert.True(open.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict, $"was {(int)open.StatusCode}");
        Assert.Equal(HttpStatusCode.NotFound, slip.StatusCode);
    }

    [Fact]
    [Trait("Spec", "PCK-6")]
    [Trait("Spec", "NFR-SC-01")]
    public async Task AScanOverHttp_AnswersInUnder300Milliseconds_AtTheNinetyFifthPercentile()
    {
        using var client = Client();
        var (id, _, gtin) = await SeedAsync(40);
        await client.PostAsync($"/api/fulfilment/orders/{id}/pick/claim", null);
        await ScanAsync(client, id, "A-01-1");
        await ScanAsync(client, id, gtin);

        var times = new List<double>();
        for (var i = 0; i < 30; i++)
        {
            var watch = Stopwatch.StartNew();
            var response = await ScanAsync(client, id, gtin);
            times.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var p95 = times.Order().ElementAt(28);
        Assert.True(p95 < 300, $"p95 was {p95:0} ms");
    }
}
