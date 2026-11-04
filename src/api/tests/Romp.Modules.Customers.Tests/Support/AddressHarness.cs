using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Infrastructure.Persistence;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Contracts;

namespace Romp.Modules.Customers.Tests.Support;

/// <summary>
/// The Customers pipeline (validation, then the transaction behaviour) over a real PostgreSQL, with a fixed geography and a settable
/// signed-in customer. Every Send uses its own scope, so concurrent calls get their own context and connection, as concurrent requests
/// do in production.
/// </summary>
internal sealed class AddressHarness : IDisposable
{
    private readonly ServiceProvider _services;

    public AddressHarness(CustomersPostgres postgres, int maxAddresses = 10, int maxWishlistItems = 200)
    {
        Geography = new FakeGeography();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICurrentActor>(new TestActor());
        services.AddSingleton<AuditSaveChangesInterceptor>();
        services.AddDbContext<CustDbContext>((sp, options) => options
            .UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CUST"))
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));
        services.AddScoped<ICustomerDbContext>(sp => sp.GetRequiredService<CustDbContext>());
        services.AddScoped<TestCurrentCustomer>();
        services.AddScoped<ICurrentCustomer>(sp => sp.GetRequiredService<TestCurrentCustomer>());
        services.AddSingleton<IGeographyQueries>(Geography);
        Catalog = new FakeCatalog();
        services.AddSingleton<IStorefrontCatalogQueries>(Catalog);
        services.AddSingleton<IStorefrontImageQueries>(new FakeImages());
        services.AddSingleton(Options.Create(new CustomersOptions { MaxAddresses = maxAddresses, MaxWishlistItems = maxWishlistItems }));

        var application = typeof(CustomersOptions).Assembly;
        services.AddValidatorsFromAssembly(application);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(application);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(CustTransactionBehavior<,>));
        });
        _services = services.BuildServiceProvider();
    }

    public FakeGeography Geography { get; }

    public FakeCatalog Catalog { get; }

    public async Task<long> NewCustomerAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustDbContext>();
        var customer = Customer.Create($"harness.{Guid.NewGuid():N}@example.test", "Har", "Ness");
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    public async Task<T> Send<T>(long? customerId, IRequest<T> request)
    {
        await using var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentCustomer>().CustomerId = customerId;
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task Send(long? customerId, IRequest request)
    {
        await using var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentCustomer>().CustomerId = customerId;
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    /// <summary>Reads the stored rows straight from the database, bypassing every handler.</summary>
    public async Task<List<(long Id, bool IsDefault, string Recipient)>> StoredAsync(long customerId)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustDbContext>();
        var rows = await db.Addresses.AsNoTracking().Where(a => a.CustomerId == customerId).OrderBy(a => a.Id).ToListAsync();
        return rows.Select(a => (a.Id, a.IsDefault, a.RecipientName)).ToList();
    }

    /// <summary>Reads the stored wishlist rows straight from the database, bypassing every handler.</summary>
    public async Task<List<(long ProductId, decimal SavedPrice)>> StoredWishlistAsync(long customerId)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustDbContext>();
        var rows = await db.Wishlist.AsNoTracking().Where(w => w.CustomerId == customerId).OrderBy(w => w.Id).ToListAsync();
        return rows.Select(w => (w.ProductId, w.SavedPrice)).ToList();
    }

    public void Dispose() => _services.Dispose();

    internal sealed class TestCurrentCustomer : ICurrentCustomer
    {
        public long? CustomerId { get; set; }
    }

    private sealed class TestActor : ICurrentActor
    {
        public string UserName => "test";
    }
}

/// <summary>Two provinces and three cities (Punjab: Lahore, Multan; Sindh: Karachi), counting how often names are fetched.</summary>
internal sealed class FakeGeography : IGeographyQueries
{
    public const short Punjab = 1;
    public const short Sindh = 2;
    public const short Lahore = 10;
    public const short Multan = 11;
    public const short Karachi = 20;

    private static readonly ProvinceInfo[] Provinces = [new(Punjab, "PB", "Punjab"), new(Sindh, "SD", "Sindh")];
    private static readonly CityInfo[] Cities = [new(Lahore, "LHE", "Lahore", Punjab), new(Multan, "MUX", "Multan", Punjab), new(Karachi, "KHI", "Karachi", Sindh)];

    public int ByIdCalls { get; private set; }

    public void ResetCalls() => ByIdCalls = 0;

    public Task<IReadOnlyList<ProvinceInfo>> GetProvincesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProvinceInfo>>(Provinces);

    public Task<IReadOnlyList<CityInfo>?> GetCitiesAsync(short provinceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CityInfo>?>(Provinces.Any(p => p.Id == provinceId) ? Cities.Where(c => c.ProvinceId == provinceId).ToList() : null);

    public Task<bool> CityBelongsToProvinceAsync(short cityId, short provinceId, CancellationToken cancellationToken) =>
        Task.FromResult(Cities.Any(c => c.Id == cityId && c.ProvinceId == provinceId));

    public Task<IReadOnlyDictionary<short, CityInfo>> GetCitiesByIdAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken)
    {
        ByIdCalls++;
        return Task.FromResult<IReadOnlyDictionary<short, CityInfo>>(Cities.Where(c => ids.Contains(c.Id)).ToDictionary(c => c.Id));
    }

    public Task<IReadOnlyDictionary<short, ProvinceInfo>> GetProvincesByIdAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken)
    {
        ByIdCalls++;
        return Task.FromResult<IReadOnlyDictionary<short, ProvinceInfo>>(Provinces.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id));
    }
}

/// <summary>A settable storefront catalogue that counts calls, so a test can prove a page is one bulk call and set prices and availability.</summary>
internal sealed class FakeCatalog : IStorefrontCatalogQueries
{
    private readonly Dictionary<long, ProductSummary> _products = [];

    public int Calls { get; private set; }

    public void Publish(long id, decimal price, decimal? was = null, bool available = true) =>
        _products[id] = new ProductSummary(id, $"Product {id}", $"/girls/dresses/product-{id}", price, was, available, id * 10);

    public void Unpublish(long id) => _products.Remove(id);

    public Task<IReadOnlyList<ProductSummary>> GetProductSummariesAsync(IReadOnlyCollection<long> productIds, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult<IReadOnlyList<ProductSummary>>(productIds.Where(_products.ContainsKey).Select(id => _products[id]).ToList());
    }
}

/** Every image has a finished 640-wide WebP and JPEG, named after its id. */
internal sealed class FakeImages : IStorefrontImageQueries
{
    public Task<IReadOnlyList<ImageThumbnail>> GetThumbnailsAsync(IReadOnlyCollection<long> imageIds, int minWidth, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImageThumbnail>>(imageIds.Select(id => new ImageThumbnail(id, $"/media/{id}-640.webp", $"/media/{id}-640.jpg", 640)).ToList());
}
