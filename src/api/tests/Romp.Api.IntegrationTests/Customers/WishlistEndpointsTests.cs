using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Customers;

/// <summary>
/// customers/wishlist W6 and W7 through the real host: the wishlist routes read the real storefront read model, ownership is by
/// construction (WSL-7, SEC-05), repeats are safe (WSL-4) and responses are never cached. The stand-in is customer one;
/// <c>X-Dev-Customer: other</c> is the seeded second customer.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class WishlistEndpointsTests(ProductionApiFixture api)
{
    private const string List = "/api/customers/me/wishlist";
    private const long Dress = 7_100_001;
    private const long Jacket = 7_100_002;
    private const long Shoes = 7_100_003;

    private async Task SeedProductsAsync()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StfrDbContext>();
        foreach (var (id, price) in new[] { (Dress, 1500m), (Jacket, 3200m), (Shoes, 900m) })
        {
            if (db.Products.Any(p => p.Id == id))
            {
                continue;
            }

            var product = new PublishedProduct(
                id, 1, $"/girls/dresses/wish-{id}", $"Wish product {id}", "A product.", $"STY-W{id}", "GIRLS", "Girls", "DRESSES", "Dresses", "KIDS", "Kids", DateTimeOffset.UtcNow, null);
            product.SetDerived(new PriceSummary(price, price, false, 0m), new ProductVisibilityResult(true, true), imageReady: true);
            product.InsrDte = DateTimeOffset.UtcNow;
            product.InsrBy = "test";
            db.Products.Add(product);
        }

        await db.SaveChangesAsync();
    }

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
        var ids = await JsonAsync(await SendAsync(HttpMethod.Get, List + "/ids", customer: customer));
        foreach (var id in ids.EnumerateArray())
        {
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"{List}/{id.GetInt64()}", customer: customer)).StatusCode);
        }
    }

    [Fact]
    [Trait("Spec", "WSL-1")]
    [Trait("Spec", "WSL-4")]
    [Trait("Spec", "WSL-8")]
    public async Task AddRepeatListAndRemove_WorkEndToEnd_AndResponsesAreNoStore()
    {
        await SeedProductsAsync();
        await ClearAsync(null);

        var added = await SendAsync(HttpMethod.Put, $"{List}/{Dress}");
        var again = await SendAsync(HttpMethod.Put, $"{List}/{Dress}");
        await SendAsync(HttpMethod.Put, $"{List}/{Jacket}");

        Assert.Equal(HttpStatusCode.NoContent, added.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        var ids = await SendAsync(HttpMethod.Get, List + "/ids");
        Assert.Equal([Jacket, Dress], (await JsonAsync(ids)).EnumerateArray().Select(e => e.GetInt64()));
        Assert.Contains("no-store", ids.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);

        var page = await JsonAsync(await SendAsync(HttpMethod.Get, List + "?pageSize=1"));
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        var first = page.GetProperty("items")[0];
        Assert.Equal(Jacket, first.GetProperty("productId").GetInt64());
        Assert.Equal(3200m, first.GetProperty("price").GetDecimal());
        Assert.Equal($"/girls/dresses/wish-{Jacket}", first.GetProperty("path").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"{List}/{Dress}")).StatusCode);
        Assert.Equal(1, (await JsonAsync(await SendAsync(HttpMethod.Get, List))).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    [Trait("Spec", "WSL-7")]
    public async Task AnotherCustomersItem_Is404AndUnchanged()
    {
        await SeedProductsAsync();
        await ClearAsync(null);
        await ClearAsync("other");
        await SendAsync(HttpMethod.Put, $"{List}/{Shoes}");

        var attempt = await SendAsync(HttpMethod.Delete, $"{List}/{Shoes}", customer: "other");

        Assert.Equal(HttpStatusCode.NotFound, attempt.StatusCode);
        Assert.Equal(1, (await JsonAsync(await SendAsync(HttpMethod.Get, List))).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await JsonAsync(await SendAsync(HttpMethod.Get, List, customer: "other"))).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    [Trait("Spec", "WSL-7")]
    public async Task NobodySignedIn_IsUnauthorized_OnEveryRoute()
    {
        var calls = new[]
        {
            (HttpMethod.Get, List), (HttpMethod.Get, List + "/ids"), (HttpMethod.Put, $"{List}/{Dress}"),
            (HttpMethod.Delete, $"{List}/{Dress}"), (HttpMethod.Post, List + "/merge"),
        };

        foreach (var (method, url) in calls)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(method, url, customer: "none")).StatusCode);
        }
    }

    [Fact]
    [Trait("Spec", "WSL-5")]
    public async Task SavingAProductThatDoesNotExist_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, $"{List}/999999999")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "WSL-3")]
    public async Task Merge_AddsTheGuestProductsOnce_AndRefusesTooManyIds()
    {
        await SeedProductsAsync();
        await ClearAsync(null);

        var first = await JsonAsync(await SendAsync(HttpMethod.Post, List + "/merge", new { productIds = new[] { Dress, Shoes, 999999999L } }));
        var second = await JsonAsync(await SendAsync(HttpMethod.Post, List + "/merge", new { productIds = new[] { Dress, Shoes } }));
        var tooMany = await SendAsync(HttpMethod.Post, List + "/merge", new { productIds = Enumerable.Range(1, 51).Select(i => (long)i).ToArray() });

        Assert.Equal(2, first.GetProperty("added").GetInt32());
        Assert.Equal(0, second.GetProperty("added").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }

    [Fact]
    [Trait("Spec", "WSL-2")]
    public async Task GuestCards_AreOpenToAnyone_ForTheRememberedIds_AndLeaveOutWhatIsNotPublished()
    {
        await SeedProductsAsync();

        var response = await SendAsync(HttpMethod.Get, $"/api/storefront/products/summaries?ids={Jacket},999999999,{Dress}", customer: "none");
        var cards = (await JsonAsync(response)).EnumerateArray().ToList();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([Jacket, Dress], cards.Select(c => c.GetProperty("productId").GetInt64()));
        Assert.Equal(3200m, cards[0].GetProperty("price").GetDecimal());
        Assert.Equal($"/girls/dresses/wish-{Jacket}", cards[0].GetProperty("path").GetString());
        Assert.Contains("public", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "WSL-2")]
    public async Task GuestCards_RefuseTooManyOrMalformedIds()
    {
        var tooMany = string.Join(',', Enumerable.Range(1, 51));

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Get, $"/api/storefront/products/summaries?ids={tooMany}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Get, "/api/storefront/products/summaries?ids=1,abc")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Get, "/api/storefront/products/summaries?ids=-3")).StatusCode);
        Assert.Empty((await JsonAsync(await SendAsync(HttpMethod.Get, "/api/storefront/products/summaries"))).EnumerateArray());
    }
}
