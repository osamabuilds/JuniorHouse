using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Cart;

/// <summary>Staff manage discount codes through the real HTTP API against PostgreSQL (cart D7, FR-57). The development host signs the caller in with every permission.</summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class DiscountAdminEndpointsTests(ProductionApiFixture api)
{
    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    private HttpClient Client() => api.Factory.CreateClient();

    private static string NewCode() => $"ADM{Random.Shared.Next(100_000, 999_999)}";

    private static object Body(string code, string kind = "percentage", decimal value = 10m, int? usageCap = null) => new
    {
        code, kind, value, minimumSpend = (decimal?)null, expiresAt = (DateTimeOffset?)null, usageCap, perCustomerCap = 1, includesSaleItems = false, categoryIds = new short[] { 3 },
    };

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    [Fact]
    [Trait("Spec", "DSC-12")]
    public async Task StaffCreateReadChangeAndSwitchOffACode()
    {
        using var client = Client();
        var code = NewCode();

        var created = await client.PostAsJsonAsync("/api/discounts", Body(code.ToLowerInvariant()), Json);
        var id = (await BodyAsync(created)).GetProperty("id").GetInt64();
        var read = await BodyAsync(await client.GetAsync($"/api/discounts/{id}"));
        var changed = await client.PutAsJsonAsync($"/api/discounts/{id}", Body(code, "fixed", 500m, usageCap: 20), Json);
        var off = await client.PutAsJsonAsync($"/api/discounts/{id}/active", new { active = false }, Json);
        var listed = await BodyAsync(await client.GetAsync("/api/discounts?active=false&pageSize=100"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(read.TryGetProperty("code", out var codeText), read.ToString());
        Assert.Equal(code, codeText.GetString());
        Assert.True(read.GetProperty("isActive").GetBoolean());
        Assert.Equal(3, read.GetProperty("categoryIds")[0].GetInt32());
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var updated = await BodyAsync(changed);
        Assert.Equal("fixed", updated.GetProperty("kind").GetString());
        Assert.Equal(20, updated.GetProperty("usageCap").GetInt32());
        Assert.False((await BodyAsync(off)).GetProperty("isActive").GetBoolean());
        Assert.Contains(listed.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetInt64() == id);
    }

    [Fact]
    [Trait("Spec", "DSC-12")]
    public async Task ADuplicateCode_AnInvalidValue_AndAnUnknownId_AreRefusedWithReasons()
    {
        using var client = Client();
        var code = NewCode();
        await client.PostAsJsonAsync("/api/discounts", Body(code), Json);

        var duplicate = await client.PostAsJsonAsync("/api/discounts", Body(code.ToLowerInvariant()), Json);
        var tooBig = await client.PostAsJsonAsync("/api/discounts", Body(NewCode(), value: 150m), Json);
        var badKind = await client.PostAsJsonAsync("/api/discounts", Body(NewCode(), "bogus"), Json);
        var missing = await client.GetAsync("/api/discounts/987654321");

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("already exists", await duplicate.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.True(tooBig.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict, tooBig.StatusCode.ToString());
        Assert.Contains("percentage", await tooBig.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, badKind.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    [Trait("Spec", "DSC-1")]
    public async Task ACodeStaffCreate_CanBeAppliedByAShopper_OnlyWhileSwitchedOn()
    {
        using var staff = Client();
        var code = NewCode();
        var id = (await BodyAsync(await staff.PostAsJsonAsync("/api/discounts", Body(code), Json))).GetProperty("id").GetInt64();
        await staff.PutAsJsonAsync($"/api/discounts/{id}/active", new { active = false }, Json);

        using var guest = api.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        guest.DefaultRequestHeaders.Add("X-Dev-Customer", "none");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/discount") { Content = JsonContent.Create(new { code }) };
        request.Headers.Add("Origin", "http://localhost");
        var response = await guest.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
