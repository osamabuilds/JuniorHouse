using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Cart;

/// <summary>
/// The admin's shipping rates through the real HTTP API against a real PostgreSQL (cart T3, TOT-5). The development host signs the caller in
/// with every permission; the permission each route needs is proved by the authorization matrix test, not here.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class ShippingEndpointsTests(ProductionApiFixture api)
{
    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    private HttpClient Client() => api.Factory.CreateClient();

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), Json);

    private async Task<int> AnyCityIdAsync()
    {
        var provinces = await BodyAsync(await Client().GetAsync("/api/geography/provinces"));
        var cities = await BodyAsync(await Client().GetAsync($"/api/geography/provinces/{provinces[0].GetProperty("id").GetInt32()}/cities"));
        return cities[0].GetProperty("id").GetInt32();
    }

    [Fact]
    [Trait("Spec", "TOT-5")]
    public async Task StaffSetTheSettings_AndTheyReadBackNoLongerUnset()
    {
        using var client = Client();

        var put = await client.PutAsJsonAsync("/api/shipping/settings", new { defaultFee = 450, freeThreshold = 8000, codFee = 100 }, Json);
        var read = await BodyAsync(await client.GetAsync("/api/shipping/settings"));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(450m, read.GetProperty("defaultFee").GetDecimal());
        Assert.Equal(8000m, read.GetProperty("freeThreshold").GetDecimal());
        Assert.Equal(100m, read.GetProperty("codFee").GetDecimal());
        Assert.False(read.GetProperty("defaultFeeIsUnset").GetBoolean());
    }

    [Fact]
    [Trait("Spec", "TOT-5")]
    public async Task StaffAddChangeListAndRemoveACityRate()
    {
        using var client = Client();
        var city = await AnyCityIdAsync();

        var added = await client.PutAsJsonAsync($"/api/shipping/rates/{city}", new { fee = 275 }, Json);
        var changed = await client.PutAsJsonAsync($"/api/shipping/rates/{city}", new { fee = 325 }, Json);
        var list = await BodyAsync(await client.GetAsync("/api/shipping/rates?pageSize=100"));
        var removed = await client.DeleteAsync($"/api/shipping/rates/{city}");
        var after = await BodyAsync(await client.GetAsync("/api/shipping/rates?pageSize=100"));

        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.Equal(325m, (await BodyAsync(changed)).GetProperty("fee").GetDecimal());
        Assert.Contains(list.GetProperty("items").EnumerateArray(), i => i.GetProperty("cityId").GetInt32() == city && i.GetProperty("fee").GetDecimal() == 325m);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.DoesNotContain(after.GetProperty("items").EnumerateArray(), i => i.GetProperty("cityId").GetInt32() == city);
    }

    [Fact]
    [Trait("Spec", "TOT-5")]
    public async Task AnUnknownCity_AndAnAbsurdFee_AreRefusedWithAReason()
    {
        using var client = Client();
        var city = await AnyCityIdAsync();

        var unknown = await client.PutAsJsonAsync("/api/shipping/rates/30000", new { fee = 100 }, Json);
        var absurd = await client.PutAsJsonAsync($"/api/shipping/rates/{city}", new { fee = 9_999_999 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.True(absurd.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest, absurd.StatusCode.ToString());
        Assert.Contains("between 0 and", await absurd.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
