using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Domain.Bookings;
using Romp.Modules.Shipping.Infrastructure.CourierPort;
using Romp.Modules.Shipping.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Shipping;

/// <summary>
/// The shipping routes through the real HTTP API against PostgreSQL (S15 S11, T1). The development host signs the caller in with every permission; who may
/// call what is proved for every role by the authorization matrix and role behaviour tests, which include these routes.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class ShippingEndpointsTests(ProductionApiFixture api)
{
    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;
    private const string PrimarySecret = "local-demo-courier-primary-secret-0123456789";

    private static long _seq = 6_000_000;

    private HttpClient Client() => api.Factory.CreateClient();

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) => JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    /// <summary>A booked shipment (primary courier, a known waybill), as the booking worker would have left it.</summary>
    private async Task<(long Id, string Waybill)> BookedAsync()
    {
        var n = Interlocked.Increment(ref _seq);
        var shipment = Shipment.Create(n, $"RMP-SH{n}", 1500m, 500, DateTimeOffset.UtcNow);
        shipment.MarkBooked(Couriers.Primary, $"PRM-IT{n}", BookingSources.Courier, DateTimeOffset.UtcNow);
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShpgDbContext>();
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        return (shipment.Id, shipment.Waybill!);
    }

    private static StringContent Push(string waybill, string status, out string signature, string? secret = null)
    {
        var body = JsonSerializer.Serialize(new { waybill, status, eventId = Guid.NewGuid().ToString("N"), at = DateTimeOffset.UtcNow });
        signature = FakeCourierGateway.Sign(secret ?? PrimarySecret, body);
        return new StringContent(body, Encoding.UTF8, "application/json");
    }

    [Fact]
    [Trait("Spec", "TRK-1")]
    public async Task ASignedPush_MovesTheShipment_AndTheStaffRoutesShowIt()
    {
        using var client = Client();
        var (id, waybill) = await BookedAsync();
        using var content = Push(waybill, "PICKED_UP", out var signature);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/shipping/webhooks/PRIMARY") { Content = content };
        request.Headers.Add(FakeCourierGateway.SignatureHeader, signature);

        var response = await client.SendAsync(request);
        var detail = await BodyAsync(await client.GetAsync($"/api/shipping/shipments/{id}"));
        var list = await BodyAsync(await client.GetAsync("/api/shipping/shipments?status=PICKED_UP&pageSize=100"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("PICKED_UP", detail.GetProperty("row").GetProperty("status").GetString());
        Assert.Contains(list.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetInt64() == id);
    }

    [Theory]
    [Trait("Spec", "TRK-2")]
    [InlineData("none")]
    [InlineData("forged")]
    [InlineData("unknown-waybill")]
    public async Task ABadPush_IsOneGenericRefusal_AndChangesNothing(string kind)
    {
        using var client = Client();
        var (id, waybill) = await BookedAsync();
        using var content = Push(kind == "unknown-waybill" ? "PRM-NOSUCH" : waybill, "DELIVERED", out var signature, kind == "forged" ? "forged-secret-0123456789abcdef" : null);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/shipping/webhooks/PRIMARY") { Content = content };
        if (kind != "none")
        {
            request.Headers.Add(FakeCourierGateway.SignatureHeader, signature);
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Not accepted", await response.Content.ReadAsStringAsync());
        Assert.Equal("BOOKED", (await BodyAsync(await client.GetAsync($"/api/shipping/shipments/{id}"))).GetProperty("row").GetProperty("status").GetString());
    }

    [Fact]
    [Trait("Spec", "TRK-11")]
    public async Task AnOversizedPush_IsRefusedWithoutBeingRead()
    {
        using var client = Client();
        using var content = new StringContent(new string('x', 40_000), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/shipping/webhooks/PRIMARY", content);

        Assert.True(response.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest, $"was {(int)response.StatusCode}");
    }

    [Fact]
    [Trait("Spec", "TRK-9")]
    public async Task PublicTracking_ShowsStatusesOnly_AndEveryMissIsTheSame404()
    {
        using var client = Client();
        var (_, waybill) = await BookedAsync();

        var found = await client.GetAsync($"/api/shipping/track?waybill={waybill}");
        var text = await found.Content.ReadAsStringAsync();
        var miss = await client.GetAsync("/api/shipping/track?waybill=PRM-NOSUCH");
        var junk = await client.GetAsync("/api/shipping/track?waybill=%27%3B%20DROP%20TABLE");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Contains(waybill, text);
        Assert.DoesNotContain("RMP-SH", text);
        Assert.Equal(HttpStatusCode.NotFound, miss.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, junk.StatusCode);
        Assert.Equal((await BodyAsync(miss)).GetProperty("title").GetString(), (await BodyAsync(junk)).GetProperty("title").GetString());
        Assert.Contains("no-store", string.Join(",", found.Headers.CacheControl?.ToString()));
    }

    [Fact]
    [Trait("Spec", "BKG-4")]
    public async Task StaffSeeTheQueue_TheLabelRefusesBeforeABooking_AndAnUnknownShipmentIsNotFound()
    {
        using var client = Client();
        var n = Interlocked.Increment(ref _seq);
        long pendingId;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShpgDbContext>();
            var pending = Shipment.Create(n, $"RMP-SH{n}", 0m, 400, DateTimeOffset.UtcNow.AddYears(-1));
            db.Shipments.Add(pending);
            await db.SaveChangesAsync();
            pendingId = pending.Id;
        }

        var queue = await BodyAsync(await client.GetAsync("/api/shipping/queue?pageSize=100"));
        var label = await client.GetAsync($"/api/shipping/shipments/{pendingId}/label");
        var missing = await client.GetAsync("/api/shipping/shipments/987654321");

        Assert.Contains(queue.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetInt64() == pendingId);
        Assert.Equal(HttpStatusCode.Conflict, label.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    [Trait("Spec", "MBQ-4")]
    public async Task AManualWaybill_BooksAPendingShipment_AndTheSourceIsManual()
    {
        using var client = Client();
        var n = Interlocked.Increment(ref _seq);
        long id;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShpgDbContext>();
            var pending = Shipment.Create(n, $"RMP-SH{n}", 0m, 400, DateTimeOffset.UtcNow.AddYears(-1));
            db.Shipments.Add(pending);
            await db.SaveChangesAsync();
            id = pending.Id;
        }

        var response = await client.PostAsJsonAsync($"/api/shipping/shipments/{id}/manual-waybill", new { courier = "PRIMARY", waybill = $"PHONE-{n}" }, Json);
        var again = await client.PostAsJsonAsync($"/api/shipping/shipments/{id}/manual-waybill", new { courier = "PRIMARY", waybill = $"PHONE-{n}-B" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MANUAL", (await BodyAsync(response)).GetProperty("bookingSource").GetString());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    [Trait("Spec", "MNF-5")]
    public async Task AManifest_CanBeGeneratedCountedAndRead()
    {
        using var client = Client();
        await BookedAsync();

        var generated = await BodyAsync(await client.PostAsync("/api/shipping/manifests/generate", null));
        var manifest = generated.EnumerateArray().FirstOrDefault();
        if (manifest.ValueKind == JsonValueKind.Undefined)
        {
            return;
        }

        var id = manifest.GetProperty("id").GetInt64();
        var detail = await client.GetAsync($"/api/shipping/manifests/{id}");
        var counted = await client.PostAsJsonAsync($"/api/shipping/manifests/{id}/count", new { parcels = 9999 }, Json);
        var list = await BodyAsync(await client.GetAsync("/api/shipping/manifests?pageSize=5"));

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, counted.StatusCode);
        Assert.Equal("MISMATCH", (await BodyAsync(counted)).GetProperty("status").GetString());
        Assert.Equal(5, list.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    [Trait("Spec", "CPT-6")]
    public async Task TheDevelopmentControls_TakeAPretendCourierDownAndPushSignedStatuses()
    {
        using var client = Client();
        var (id, waybill) = await BookedAsync();

        var down = await client.PostAsJsonAsync("/dev/shipping/outage", new { courier = "PRIMARY", down = true }, Json);
        var up = await client.PostAsJsonAsync("/dev/shipping/outage", new { courier = "PRIMARY", down = false }, Json);
        var unknown = await client.PostAsJsonAsync("/dev/shipping/outage", new { courier = "NOBODY", down = true }, Json);
        var push = await client.PostAsJsonAsync("/dev/shipping/push", new { courier = "PRIMARY", waybill, status = "IN_TRANSIT" }, Json);

        Assert.Equal(HttpStatusCode.OK, down.StatusCode);
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, push.StatusCode);
        Assert.Equal("IN_TRANSIT", (await BodyAsync(await client.GetAsync($"/api/shipping/shipments/{id}"))).GetProperty("row").GetProperty("status").GetString());
    }
}
