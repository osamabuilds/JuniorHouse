using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Identity;

/// <summary>
/// The audit trail through the real host (identity/audit-trail AC-3, AC-4, AC-8, AC-12): a change in another module reaches the
/// trail through its outbox and Identity's handler, the list answers it, and no route can change an entry.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class AuditEndpointsTests(ProductionApiFixture api)
{
    private static long _styleSeed = 8_100;

    [Fact]
    [Trait("Spec", "FR-59")]
    [Trait("Spec", "AC-3")]
    [Trait("Spec", "AC-8")]
    public async Task AChangeInAnotherModule_ReachesTheTrail_ThroughItsOutbox()
    {
        var n = Interlocked.Increment(ref _styleSeed);
        long id;
        string table;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var style = new Style($"STY-AUD-{n}", $"Audit Dress {n}", null, 3, 2, 3, 1, 450m, 1200m);
            style.SetColourSizeAndTargets([1], [4, 5], []);
            db.Styles.Add(style);
            await db.SaveChangesAsync();
            id = style.Id;
            table = db.Model.FindEntityType(typeof(Style))!.GetTableName()!;
        }

        JsonElement found = default;
        for (var attempt = 0; attempt < 100 && found.ValueKind != JsonValueKind.Object; attempt++)
        {
            await api.DispatchAsync();
            var page = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/identity/audit?module=CTLG&entityType={table}&entityKey={id}"));
            if (page.GetProperty("totalCount").GetInt32() > 0)
            {
                found = page.GetProperty("items")[0];
            }
            else
            {
                await Task.Delay(100);
            }
        }

        Assert.Equal(JsonValueKind.Object, found.ValueKind);
        Assert.Equal("INSERT", found.GetProperty("operation").GetString());
        Assert.Equal("system", found.GetProperty("actor").GetString());
        Assert.NotEmpty(found.GetProperty("columns").EnumerateArray());

        // Names only: the style code we just wrote is nowhere in the answer.
        var raw = found.GetRawText();
        Assert.DoesNotContain($"STY-AUD-{n}", raw, StringComparison.Ordinal);
        Assert.DoesNotContain($"Audit Dress {n}", raw, StringComparison.Ordinal);

        var detail = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/identity/audit/{found.GetProperty("id").GetInt64()}"));
        Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture), detail.GetProperty("entityKey").GetString());
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    [Trait("Spec", "AC-12")]
    public async Task NoWriteRoutesExist()
    {
        var routes = api.Services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/identity/audit", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        Assert.NotEmpty(routes);

        var methods = routes.SelectMany(r => r.Metadata.OfType<HttpMethodMetadata>()).SelectMany(m => m.HttpMethods).Distinct().ToList();
        Assert.Equal(["GET"], methods);

        // Whatever a caller tries, nothing but a read is routed.
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, "/api/identity/audit/1") { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
            var response = await api.Client.SendAsync(request);
            Assert.True(response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, $"{method} answered {(int)response.StatusCode}");
        }
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task ARangeOver366Days_IsABadRequest()
    {
        var response = await api.Client.GetAsync("/api/identity/audit?from=2024-01-01T00:00:00Z&to=2026-01-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
