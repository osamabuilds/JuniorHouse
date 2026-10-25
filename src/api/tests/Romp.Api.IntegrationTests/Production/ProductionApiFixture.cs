using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Production.Infrastructure.Persistence;
using Romp.Modules.Reference.Infrastructure.Persistence;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Api.IntegrationTests.Production;

/// <summary>
/// The real API host against a real PostgreSQL (Testcontainers): every module migrated, the dispatcher
/// driven by hand (<see cref="DispatchAsync"/>) instead of waiting on its timer. Used by the Sprint 3
/// HTTP tests, which walk the whole PO -&gt; production -&gt; delivery -&gt; close flow through the public API.
/// </summary>
public sealed class ProductionApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory => _factory!;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public HttpClient Client { get; private set; } = null!;

    public IServiceProvider Services => _factory!.Services;

    /// <summary>Staff-entered dates are Pakistan time (UTC+5); every date in the tests is "today" so it is inside the send-date..today bound.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "romp-it-" + Guid.NewGuid().ToString("N"));

    public long StyleId { get; private set; }

    public long VendorId { get; private set; }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Program.cs reads these while building the host, so they are environment variables (process-wide).
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Storage__RootPath", StorageRoot);

        // Several suites hammer the API (1,000 scans for a latency check, dozens of strict-policy writes). The limiter itself is tested in
        // Romp.BuildingBlocks.Web.Tests; here it must not starve unrelated tests that share the host and its single client address.
        Environment.SetEnvironmentVariable("RateLimit__PerMinute", "1000000");
        Environment.SetEnvironmentVariable("RateLimit__StrictPerMinute", "1000000");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        Client = _factory.CreateClient();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReferenceDbContext>().Database.MigrateAsync();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await catalog.Database.MigrateAsync();
        var vendor = scope.ServiceProvider.GetRequiredService<VendorDbContext>();
        await vendor.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<ProductionDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Romp.Modules.Quality.Infrastructure.Persistence.QcDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Romp.Modules.Labelling.Infrastructure.Persistence.SkuDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Romp.Modules.Warehouse.Infrastructure.Persistence.WhseDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Romp.Modules.Storefront.Infrastructure.Persistence.StfrDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Romp.Modules.Identity.Infrastructure.Persistence.IdntDbContext>().Database.MigrateAsync();

        var style = new Style("STY-IT-001", "Integration Dress", "Test", categoryId: 3, genderId: 2, ageBracketId: 3, fabricId: 1, targetUnitCost: 450m, targetRetailPrice: 1200m);
        style.SetColourSizeAndTargets([1, 2], [4, 5], [(4, 1, 50), (4, 2, 40), (5, 1, 30), (5, 2, 25)]);
        catalog.Styles.Add(style);
        await catalog.SaveChangesAsync();
        StyleId = style.Id;

        var vendorRow = new Romp.Modules.Vendor.Domain.Vendors.Vendor("IT Garments", "Ali Raza", "+92-300-1234567", "ali@example.pk", cityId: 7, paymentTermId: 2);
        vendorRow.SetSpecialisations([1]);
        vendor.Vendors.Add(vendorRow);
        await vendor.SaveChangesAsync();
        VendorId = vendorRow.Id;
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
        Environment.SetEnvironmentVariable("Storage__RootPath", null);
        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
    }

    /// <summary>Runs the dispatcher until nothing more is delivered (an event handled here can enqueue the next hop).</summary>
    public async Task DispatchAsync()
    {
        // The background dispatcher polls too (every second), so a pass here can find nothing to claim while the
        // background one holds the messages and is about to enqueue the next hop. Keep going until the outbox is idle.
        var dispatcher = Services.GetRequiredService<IOutboxDispatcher>();
        var health = Services.GetRequiredService<OutboxHealthReader>();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var quietPasses = 0;
        while (DateTime.UtcNow < deadline && quietPasses < 3)
        {
            var delivered = await dispatcher.DispatchOnceAsync(CancellationToken.None);
            var idle = (await health.ReadAsync(CancellationToken.None)).All(h => h.Pending == 0 && h.InFlight == 0);
            quietPasses = delivered == 0 && idle ? quietPasses + 1 : 0;
            if (quietPasses < 3)
            {
                await Task.Delay(50);
            }
        }
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {body}");
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    public Task<HttpResponseMessage> PostAsync(string url, object body) => Client.PostAsJsonAsync(url, body, Json);

    /// <summary>Creates a PO with two lines (size 4 / colour 1 x 100, size 5 / colour 1 x 50), sends and acknowledges it, and waits for its production run to open.</summary>
    public async Task<(long PoId, string PoNo, long RunId)> AcknowledgedPoWithRunAsync(short fabricResponsibilityId = 1)
    {
        var created = await ReadAsync<JsonElement>(await PostAsync("/api/purchase-orders", new
        {
            vendorId = VendorId,
            styleId = StyleId,
            unitCost = 450m,
            expectedDeliveryDate = Today.AddDays(20),
            lines = new[] { new { sizeId = 4, colourId = 1, qty = 100 }, new { sizeId = 5, colourId = 1, qty = 50 } },
        }));
        var poId = created.GetProperty("id").GetInt64();
        var poNo = created.GetProperty("poNo").GetString()!;

        var updated = await Client.PutAsJsonAsync($"/api/purchase-orders/{poId}", new
        {
            unitCost = 450m,
            expectedDeliveryDate = Today.AddDays(20),
            paymentTermId = created.GetProperty("paymentTermId").GetInt16(),
            advancePercent = created.GetProperty("advancePercent").GetDecimal(),
            lines = new[] { new { sizeId = 4, colourId = 1, qty = 100 }, new { sizeId = 5, colourId = 1, qty = 50 } },
            latestAcceptableDate = Today.AddDays(30),
            overTolerancePercent = 5m,
            underTolerancePercent = 5m,
            fabricResponsibilityId,
        }, Json);
        Assert.True(updated.IsSuccessStatusCode, await updated.Content.ReadAsStringAsync());

        Assert.True((await Client.PostAsync($"/api/purchase-orders/{poId}/send?sendWithoutTechPack=true", null)).IsSuccessStatusCode);
        Assert.True((await Client.PostAsync($"/api/purchase-orders/{poId}/acknowledge", null)).IsSuccessStatusCode);

        await DispatchAsync();
        var run = await ReadAsync<JsonElement>(await Client.GetAsync($"/api/production/runs/by-po/{poId}"));
        return (poId, poNo, run.GetProperty("id").GetInt64());
    }

    public async Task<short> PoStatusAsync(long poId) =>
        (await ReadAsync<JsonElement>(await Client.GetAsync($"/api/purchase-orders/{poId}"))).GetProperty("statusId").GetInt16();
}

[CollectionDefinition(Name)]
public sealed class ProductionApiCollectionDefinition : ICollectionFixture<ProductionApiFixture>
{
    public const string Name = "Production API";
}
