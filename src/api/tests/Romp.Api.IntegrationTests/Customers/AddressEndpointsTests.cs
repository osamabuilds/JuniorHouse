using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Customers;

/// <summary>
/// customers/addresses A5 through the real host: the address book routes, ownership by construction (ADD-3, SEC-05), the limit and the
/// field errors. The development stand-in is customer one; <c>X-Dev-Customer: other</c> is the seeded second customer.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class AddressEndpointsTests(ProductionApiFixture api)
{
    private const string Book = "/api/customers/me/addresses";

    private static object Body(string name = "Ayesha Khan", string phone = "0300 1234567", int province = 1, int city = 2) => new
    {
        recipientName = name,
        phone,
        provinceId = province,
        cityId = city,
        lineOne = "House 12, Street 4",
        lineTwo = (string?)null,
        postcode = "54000",
    };

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, string? customer = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (customer is not null)
        {
            request.Headers.Add("X-Dev-Customer", customer);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: ProductionApiFixture.Json);
        }

        return await api.Client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), ProductionApiFixture.Json);

    private async Task ClearAsync(string? customer)
    {
        var list = await JsonAsync(await SendAsync(HttpMethod.Get, Book + "?pageSize=100", customer: customer));
        foreach (var item in list.GetProperty("items").EnumerateArray())
        {
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"{Book}/{item.GetProperty("id").GetInt64()}", customer: customer)).StatusCode);
        }
    }

    [Fact]
    [Trait("Spec", "ADD-1")]
    [Trait("Spec", "ADD-2")]
    public async Task AddEditMakeDefaultAndRemove_WorkEndToEnd_AndResponsesAreNoStore()
    {
        await ClearAsync(null);

        var created = await SendAsync(HttpMethod.Post, Book, Body("First Home"));
        var first = await JsonAsync(created);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(first.GetProperty("isDefault").GetBoolean());
        Assert.Equal("+923001234567", first.GetProperty("phone").GetString());
        Assert.False(string.IsNullOrEmpty(first.GetProperty("cityName").GetString()));
        Assert.Contains("no-store", created.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);

        var second = await JsonAsync(await SendAsync(HttpMethod.Post, Book, Body("Second Home")));
        var secondId = second.GetProperty("id").GetInt64();
        Assert.False(second.GetProperty("isDefault").GetBoolean());

        var edited = await SendAsync(HttpMethod.Put, $"{Book}/{secondId}", Body("Renamed Home"));
        Assert.Equal("Renamed Home", (await JsonAsync(edited)).GetProperty("recipientName").GetString());

        var made = await SendAsync(HttpMethod.Post, $"{Book}/{secondId}/default");
        Assert.True((await JsonAsync(made)).GetProperty("isDefault").GetBoolean());

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, Book));
        Assert.Equal(2, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(secondId, list.GetProperty("items")[0].GetProperty("id").GetInt64());

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"{Book}/{secondId}")).StatusCode);
        var after = await JsonAsync(await SendAsync(HttpMethod.Get, Book));
        Assert.Equal(1, after.GetProperty("totalCount").GetInt32());
        Assert.True(after.GetProperty("items")[0].GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    [Trait("Spec", "ADD-3")]
    [Trait("Spec", "SEC-05")]
    public async Task AnotherCustomersAddress_Is404ForReadEditRemoveAndDefault()
    {
        var mine = await JsonAsync(await SendAsync(HttpMethod.Post, Book, Body("Mine Only")));
        var id = mine.GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"{Book}/{id}", customer: "other")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, $"{Book}/{id}", Body("Hacked"), customer: "other")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, $"{Book}/{id}/default", customer: "other")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, $"{Book}/{id}", customer: "other")).StatusCode);

        var still = await JsonAsync(await SendAsync(HttpMethod.Get, $"{Book}/{id}"));
        Assert.Equal("Mine Only", still.GetProperty("recipientName").GetString());
        var theirs = await JsonAsync(await SendAsync(HttpMethod.Get, Book, customer: "other"));
        Assert.DoesNotContain(theirs.GetProperty("items").EnumerateArray(), a => a.GetProperty("id").GetInt64() == id);
    }

    [Fact]
    [Trait("Spec", "ADD-4")]
    [Trait("Spec", "ADD-5")]
    public async Task InvalidInput_Is400_WithTheFieldNames()
    {
        var response = await SendAsync(HttpMethod.Post, Book, Body(phone: "12345", province: 1, city: 20_000));

        var body = await JsonAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Phone", out _) || errors.TryGetProperty("phone", out _));
        Assert.True(errors.TryGetProperty("CityId", out _) || errors.TryGetProperty("cityId", out _));
    }

    [Fact]
    [Trait("Spec", "ADD-6")]
    public async Task TheEleventhAddress_IsRefusedWithAClearMessage()
    {
        await ClearAsync("other");
        for (var i = 1; i <= 10; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await SendAsync(HttpMethod.Post, Book, Body($"Home {i}"), customer: "other")).StatusCode);
        }

        var refused = await SendAsync(HttpMethod.Post, Book, Body("Home 11"), customer: "other");

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("10", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await ClearAsync("other");
    }

    [Fact]
    [Trait("Spec", "ADD-7")]
    public async Task TheList_IsPagedAndTheSizeIsClamped()
    {
        await ClearAsync(null);
        for (var i = 1; i <= 3; i++)
        {
            await SendAsync(HttpMethod.Post, Book, Body($"Home {i}"));
        }

        var page = await JsonAsync(await SendAsync(HttpMethod.Get, Book + "?page=2&pageSize=2"));
        var huge = await JsonAsync(await SendAsync(HttpMethod.Get, Book + "?pageSize=5000"));

        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(100, huge.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    [Trait("Spec", "SEC-05")]
    public async Task AGuest_Gets401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, Book, customer: "none")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Post, Book, Body(), customer: "none")).StatusCode);
    }
}
