using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Domain.NotifyMe;
using Romp.Modules.Customers.Infrastructure.Persistence;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Customers;

/// <summary>
/// customers/notify-me N8 through the real host (NTM-2, NTM-3, NTM-6, NTM-7, NTM-11, NTM-12, NTM-13): the public form checks the variant against
/// the real storefront read model, answers the same for new and repeated addresses, and is never cached; the staff list shows counts only.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class NotifyMeEndpointsTests(ProductionApiFixture api)
{
    private const long ProductId = 7_200_001;
    private const long SoldOut = 7_200_011;
    private const long InStock = 7_200_012;

    private static string NewEmail() => $"http.ntm.{Guid.NewGuid():N}@example.test";

    private async Task SeedAsync()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StfrDbContext>();
        if (await db.Products.AnyAsync(p => p.Id == ProductId))
        {
            return;
        }

        var product = new PublishedProduct(
            ProductId, 1, "/girls/dresses/notify-dress", "Notify dress", "A dress.", "STY-NTM", "GIRLS", "Girls", "DRESSES", "Dresses", "KIDS", "Kids", DateTimeOffset.UtcNow, null);
        product.SetDerived(new PriceSummary(1500m, 1500m, false, 0m), new ProductVisibilityResult(true, true), imageReady: true);
        product.InsrDte = DateTimeOffset.UtcNow;
        product.InsrBy = "test";
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var soldOut = new VariantRow(SoldOut, ProductId, 1, "NAVY", "Navy", 1, "4-5Y", "4-5 years", 1, "SKU-NTM-1", 1500m, null, true);
        soldOut.ApplyStock(0, 1);
        var inStock = new VariantRow(InStock, ProductId, 1, "NAVY", "Navy", 2, "6-7Y", "6-7 years", 2, "SKU-NTM-2", 1500m, null, true);
        inStock.ApplyStock(5, 1);
        foreach (var variant in new[] { soldOut, inStock })
        {
            variant.InsrDte = DateTimeOffset.UtcNow;
            variant.InsrBy = "test";
            db.Variants.Add(variant);
        }

        await db.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, string? origin = null)
    {
        await SeedAsync();
        using var request = new HttpRequestMessage(method, url);
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: ProductionApiFixture.Json);
        }

        return await api.Client.SendAsync(request);
    }

    private async Task<int> OpenCountAsync(string email)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustDbContext>();
        return await db.NotifyRequests.AsNoTracking().CountAsync(r => r.Email == email && r.Status == NotifyStatus.Open);
    }

    [Fact]
    [Trait("Spec", "NTM-2")]
    public async Task ASoldOutVariant_IsRecorded_AndARepeatGetsTheSameAnswer_NoStore()
    {
        var email = NewEmail();

        var first = await SendAsync(HttpMethod.Post, "/api/notify-me", new { variantId = SoldOut, email });
        var again = await SendAsync(HttpMethod.Post, "/api/notify-me", new { variantId = SoldOut, email = email.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await again.Content.ReadAsStringAsync());
        Assert.Equal("no-store", first.Headers.CacheControl?.ToString());
        Assert.Equal(1, await OpenCountAsync(email));
    }

    [Fact]
    [Trait("Spec", "NTM-6")]
    public async Task AnInStockVariant_AnUnknownOne_AndABadAddress_AreRefusedWith400()
    {
        var email = NewEmail();

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, "/api/notify-me", new { variantId = InStock, email })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, "/api/notify-me", new { variantId = 999_999_999, email })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, "/api/notify-me", new { variantId = SoldOut, email = "nope" })).StatusCode);
        Assert.Equal(0, await OpenCountAsync(email));
    }

    [Fact]
    [Trait("Spec", "NTM-7")]
    public async Task APostFromAnotherSite_IsRefused()
    {
        var email = NewEmail();

        var response = await SendAsync(HttpMethod.Post, "/api/notify-me", new { variantId = SoldOut, email }, origin: "https://evil.example");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await OpenCountAsync(email));
    }

    [Fact]
    [Trait("Spec", "NTM-7")]
    public void ThePublicRoute_IsOnTheStrictRateLimit()
    {
        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/notify-me", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, e => Assert.Contains(e.Metadata.GetOrderedMetadata<EnableRateLimitingAttribute>(), a => a.PolicyName == RateLimitPolicies.Strict));
    }

    [Fact]
    [Trait("Spec", "NTM-11")]
    public async Task TheStaffList_ShowsTheCounts_AndNeverAnAddress()
    {
        var email = NewEmail();
        await SendAsync(HttpMethod.Post, "/api/notify-me", new { variantId = SoldOut, email });

        var response = await SendAsync(HttpMethod.Get, "/api/admin/customers/notify-demand?page=1&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(text, ProductionApiFixture.Json);
        var row = json.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("variantId").GetInt64() == SoldOut);
        Assert.True(row.GetProperty("waiting").GetInt32() >= 1);
        Assert.Equal("Notify dress", row.GetProperty("productName").GetString());
        Assert.DoesNotContain("@", text, StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "NTM-13")]
    public async Task TheStaffListPageSize_IsClampedTo100()
    {
        var json = JsonSerializer.Deserialize<JsonElement>(
            await (await SendAsync(HttpMethod.Get, "/api/admin/customers/notify-demand?pageSize=100000")).Content.ReadAsStringAsync(), ProductionApiFixture.Json);

        Assert.Equal(100, json.GetProperty("pageSize").GetInt32());
    }
}
