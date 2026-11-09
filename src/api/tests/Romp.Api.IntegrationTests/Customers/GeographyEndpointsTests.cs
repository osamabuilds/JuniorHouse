using System.Net;
using System.Text.Json;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Customers;

/// <summary>customers/addresses A1 (ADD-4): the province and city lists the address form fills its dependent selects from. Anonymous and cacheable.</summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class GeographyEndpointsTests(ProductionApiFixture api)
{
    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task Provinces_AreListedForAnonymousCallers_AndAreCacheable()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/geography/provinces");
        request.Headers.Add("X-Dev-Customer", "none");

        var response = await api.Client.SendAsync(request);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), ProductionApiFixture.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, body.GetArrayLength());
        Assert.Equal("Punjab", body[0].GetProperty("name").GetString());
        Assert.True(response.Headers.CacheControl?.Public);
        Assert.True(response.Headers.CacheControl?.MaxAge > TimeSpan.Zero);
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task Cities_AreListedForAProvince_AndBelongToIt()
    {
        var provinces = JsonSerializer.Deserialize<JsonElement>(await api.Client.GetStringAsync("/api/geography/provinces"), ProductionApiFixture.Json);
        var sindh = provinces.EnumerateArray().Single(p => p.GetProperty("code").GetString() == "SD").GetProperty("id").GetInt32();

        var cities = JsonSerializer.Deserialize<JsonElement>(await api.Client.GetStringAsync($"/api/geography/provinces/{sindh}/cities"), ProductionApiFixture.Json);

        var names = cities.EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToList();
        Assert.Contains("Karachi", names);
        Assert.DoesNotContain("Lahore", names);
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    public async Task Cities_ForAnUnknownProvince_Is404()
    {
        var response = await api.Client.GetAsync("/api/geography/provinces/31000/cities");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
