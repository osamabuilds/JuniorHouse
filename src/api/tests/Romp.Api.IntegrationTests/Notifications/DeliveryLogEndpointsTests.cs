using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Notifications;

/// <summary>
/// The staff delivery log through the real HTTP API against PostgreSQL (notifications G3). The development host signs the caller in with every
/// permission; who may call what is proved for every role by the authorization matrix tests (DLG-4), which now include these routes.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class DeliveryLogEndpointsTests(ProductionApiFixture api)
{
    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    private HttpClient Client() => api.Factory.CreateClient();

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    private static string NewKey() => "it-" + Guid.NewGuid().ToString("N");

    /// <summary>Saves a notification with one email delivery and puts it in the given lookup status (1 queued, 2 sent, 4 failed).</summary>
    private async Task<long> SeedAsync(short statusId)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NtfyDbContext>();
        var now = DateTimeOffset.UtcNow;
        var notification = new Notification(NotificationKind.OrderConfirmation, ReferenceKinds.Order, Random.Shared.NextInt64(900_000_000, 999_999_999), Guid.NewGuid(), now, TimeSpan.FromDays(7), 0);
        notification.Plan(new ChannelPlanResult(true, false, null), now);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        var id = notification.Deliveries.Single().Id;
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE \"NTFY\".\"NTFY_CHNL\" SET \"CHNL_STS_ID\" = {0}, \"NXT_ATMP_DTE\" = NULL WHERE \"ID\" = {1}", statusId, id);
        return id;
    }

    [Fact]
    [Trait("Spec", "DLG-1")]
    [Trait("Spec", "NFR-FT-07")]
    public async Task TheLogListsDeliveriesPaged_AndTheSummaryAndDetailAnswer()
    {
        using var client = Client();
        var id = await SeedAsync(4);

        var list = await BodyAsync(await client.GetAsync("/api/admin/notifications?status=FAIL&pageSize=100"));
        var summary = await BodyAsync(await client.GetAsync("/api/admin/notifications/summary"));
        var detail = await client.GetAsync($"/api/admin/notifications/{id}");
        var missing = await client.GetAsync("/api/admin/notifications/987654321");

        var row = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetInt64() == id);
        Assert.Equal("FAIL", row.GetProperty("status").GetString());
        Assert.Equal("EMAL", row.GetProperty("channel").GetString());
        Assert.True(row.GetProperty("canResend").GetBoolean());
        Assert.True(summary.GetProperty("countsByStatus").GetProperty("FAIL").GetInt32() >= 1);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    [Trait("Spec", "DLG-2")]
    [Trait("Spec", "DLG-8")]
    [Trait("Spec", "TC-NOTIF-06")]
    public async Task ResendQueuesOnce_AndTheSameKeyAgainAnswersTheSameDelivery()
    {
        using var client = Client();
        var id = await SeedAsync(4);
        var key = NewKey();

        var first = await client.PostAsJsonAsync($"/api/admin/notifications/{id}/resend", new { key }, Json);
        var again = await client.PostAsJsonAsync($"/api/admin/notifications/{id}/resend", new { key }, Json);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal((await BodyAsync(first)).GetProperty("id").GetInt64(), (await BodyAsync(again)).GetProperty("id").GetInt64());
    }

    [Fact]
    [Trait("Spec", "DLG-7")]
    public async Task ResendOfASentMessage_AsksForConfirmationFirst()
    {
        using var client = Client();
        var id = await SeedAsync(2);
        var key = NewKey();

        var warned = await client.PostAsJsonAsync($"/api/admin/notifications/{id}/resend", new { key }, Json);
        var confirmed = await client.PostAsJsonAsync($"/api/admin/notifications/{id}/resend", new { key, acknowledgeDuplicate = true }, Json);

        Assert.Equal(HttpStatusCode.Conflict, warned.StatusCode);
        Assert.Equal("duplicate-warning", (await BodyAsync(warned)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Accepted, confirmed.StatusCode);
    }

    [Fact]
    [Trait("Spec", "DLG-2")]
    public async Task ResendRefusesAQueuedMessage_AnUnknownOne_AndABadKey()
    {
        using var client = Client();
        var queued = await SeedAsync(1);
        var failed = await SeedAsync(4);

        var notResendable = await client.PostAsJsonAsync($"/api/admin/notifications/{queued}/resend", new { key = NewKey() }, Json);
        var unknown = await client.PostAsJsonAsync("/api/admin/notifications/987654321/resend", new { key = NewKey() }, Json);
        var badKey = await client.PostAsJsonAsync($"/api/admin/notifications/{failed}/resend", new { key = "x" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, notResendable.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badKey.StatusCode);
    }
}
