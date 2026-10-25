using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Storefront.Application;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.StorefrontSettings;
using Romp.Modules.Storefront.Domain.StorefrontSettings;
using Romp.Modules.Storefront.Infrastructure.Persistence;
using Romp.Modules.Storefront.Infrastructure.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Storefront.Tests.Support;

/// <summary>
/// One real PostgreSQL (Testcontainers, CLAUDE.md) for the collection. The STFR migrations run once into a template database; each test
/// gets its own copy, so tests change the one settings draft without touching each other.
/// </summary>
public sealed class StorefrontPostgres : IAsyncLifetime
{
    private const string Template = "stfr_template";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await ExecuteAsync(_postgres.GetConnectionString(), $"CREATE DATABASE \"{Template}\"");

        await using (var context = NewContext(ConnectionStringFor(Template)))
        {
            await context.Database.MigrateAsync();
        }

        NpgsqlConnection.ClearAllPools(); // a template database must have no open connections
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync().AsTask();

    /// <summary>A fresh, migrated and seeded database; returns its connection string.</summary>
    public async Task<string> NewDatabaseAsync()
    {
        var name = "t_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(_postgres.GetConnectionString(), $"CREATE DATABASE \"{name}\" TEMPLATE \"{Template}\"");
        return ConnectionStringFor(name);
    }

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database, Pooling = true, MaxPoolSize = 5 }.ConnectionString;

    internal static StfrDbContext NewContext(string connectionString, QueryCounter? counter = null)
    {
        var builder = new DbContextOptionsBuilder<StfrDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "STFR"));
        if (counter is not null)
        {
            builder.AddInterceptors(counter);
        }

        return new StfrDbContext(builder.Options);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class StorefrontPostgresFixtureDefinition : ICollectionFixture<StorefrontPostgres>
{
    public const string Name = "Storefront PostgreSQL";
}

internal sealed class FakeMenuSettings : Romp.Modules.Storefront.Application.Navigation.IMenuSettings
{
    public IReadOnlySet<string> HiddenGenderCodes { get; } = new HashSet<string> { "UNISEX" };
}

internal sealed class FakeExternalHostPolicy : IExternalHostPolicy
{
    public IReadOnlyCollection<string> AllowedHosts { get; } = ["wa.me", "instagram.com"];
}

/// <summary>The same MediatR + FluentValidation pipeline <c>StorefrontModule</c> wires in production, against the real PostgreSQL.</summary>
internal sealed class StorefrontHost : IDisposable
{
    private readonly ServiceProvider _root;

    private StorefrontHost(string connectionString)
    {
        ConnectionString = connectionString;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();
        services.AddSingleton(NullLoggerFactory.Instance);

        services.AddDbContext<StfrDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "STFR"))
            .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<OutboxSaveChangesInterceptor>()));
        services.AddScoped<IStorefrontDbContext>(sp => sp.GetRequiredService<StfrDbContext>());
        services.AddScoped<IStorefrontReadDbContext>(sp => sp.GetRequiredService<StfrDbContext>());
        services.AddSingleton<IExternalHostPolicy, FakeExternalHostPolicy>();
        services.AddSingleton<Romp.Modules.Storefront.Application.PublishedCatalog.IReadModelSettings, FakeReadModelSettings>();
        services.AddSingleton<InMemoryFileStorage>();
        services.AddSingleton<Romp.BuildingBlocks.Storage.IFileStorage>(sp => sp.GetRequiredService<InMemoryFileStorage>());
        services.AddSingleton<FakeImageResizer>();
        services.AddSingleton<Romp.Modules.Storefront.Application.PublishedCatalog.IImageResizer>(sp => sp.GetRequiredService<FakeImageResizer>());
        services.AddSingleton<FakeCatalogLookups>();
        services.AddSingleton<Romp.Modules.Reference.Contracts.ICatalogLookupQueries>(sp => sp.GetRequiredService<FakeCatalogLookups>());
        services.AddSingleton<FakeProducers>();
        services.AddSingleton<Romp.Modules.Catalog.Contracts.IPublishedProductQueries>(sp => sp.GetRequiredService<FakeProducers>());
        services.AddSingleton<Romp.Modules.Catalog.Contracts.ISizeChartQueries>(sp => sp.GetRequiredService<FakeProducers>());
        services.AddSingleton<Romp.Modules.Warehouse.Contracts.IStockAvailabilityQueries>(sp => sp.GetRequiredService<FakeProducers>());
        services.AddScoped<IOutboxMessageHandler<Romp.Modules.Catalog.Contracts.ProductPublishedEvent>, ProductPublishedConsumer>();
        services.AddScoped<IOutboxMessageHandler<Romp.Modules.Catalog.Contracts.ProductChangedEvent>, ProductChangedConsumer>();
        services.AddScoped<IOutboxMessageHandler<Romp.Modules.Catalog.Contracts.ProductUnpublishedEvent>, ProductUnpublishedConsumer>();
        services.AddScoped<IOutboxMessageHandler<Romp.Modules.Catalog.Contracts.ProductArchivedEvent>, ProductArchivedConsumer>();
        services.AddScoped<IOutboxMessageHandler<Romp.Modules.Catalog.Contracts.SizeChartChangedEvent>, SizeChartChangedConsumer>();
        services.AddScoped<IOutboxMessageHandler<Romp.Modules.Warehouse.Contracts.StockLevelChangedEvent>, StockLevelChangedConsumer>();
        services.AddScoped<IOutboxMessageHandler<Romp.Modules.Warehouse.Contracts.StockPutawayConfirmedEvent>, StockPutawayConfirmedConsumer>();
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<IPublishedDocumentCache, PublishedDocumentCache>();
        services.AddSingleton<Romp.Modules.Storefront.Application.Navigation.IMenuSettings, FakeMenuSettings>();
        services.AddSingleton<Romp.Modules.Storefront.Application.Listings.IListingSettings, FakeListingSettings>();
        services.AddSingleton<FakePathQueries>();
        services.AddSingleton<Romp.Modules.Catalog.Contracts.IProductPathQueries>(sp => sp.GetRequiredService<FakePathQueries>());
        services.AddSingleton<FakeProductPageMetrics>();
        services.AddSingleton<Romp.Modules.Storefront.Application.ProductPages.IProductPageMetrics>(sp => sp.GetRequiredService<FakeProductPageMetrics>());
        services.AddSingleton<Romp.Modules.Storefront.Application.Abstractions.ICatalogLookupCache, Romp.Modules.Storefront.Infrastructure.CatalogLookupCache>();
        services.AddScoped<Romp.Modules.Storefront.Contracts.IStorefrontPageQueries, PublishedPageSitemapSource>();

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(StorefrontTransactionBehavior<,>));
        });

        _root = services.BuildServiceProvider();
    }

    public string ConnectionString { get; }

    public IPublishedDocumentCache Cache => _root.GetRequiredService<IPublishedDocumentCache>();

    /// <summary>The operator verb <c>rebuild-storefront</c>, run against this host; returns its exit code and what it printed.</summary>
    public async Task<(int ExitCode, string Output)> RunOperatorRebuildAsync()
    {
        var output = new StringWriter();
        var exit = await StorefrontOperator.RebuildAsync(_root, output, default);
        return (exit, output.ToString());
    }

    public InMemoryFileStorage Storage => _root.GetRequiredService<InMemoryFileStorage>();

    public FakeImageResizer Resizer => _root.GetRequiredService<FakeImageResizer>();

    public FakeCatalogLookups Lookups => _root.GetRequiredService<FakeCatalogLookups>();

    public FakeProducers Producers => _root.GetRequiredService<FakeProducers>();

    public FakePathQueries Paths => _root.GetRequiredService<FakePathQueries>();

    public FakeProductPageMetrics PageMetrics => _root.GetRequiredService<FakeProductPageMetrics>();

    /// <summary>Delivers an event the way the dispatcher does: a scope, the outbox row id in the message context, then every handler of that event type.</summary>
    public async Task DeliverAsync<TEvent>(long outboxMessageId, TEvent domainEvent)
        where TEvent : class
    {
        await using var scope = _root.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxMessageContext>();
        context.MessageId = outboxMessageId;
        context.Schema = "STFR";
        foreach (var handler in scope.ServiceProvider.GetServices<IOutboxMessageHandler<TEvent>>())
        {
            await handler.HandleAsync(domainEvent, default);
        }
    }

    public static async Task<StorefrontHost> CreateAsync(StorefrontPostgres postgres) => new(await postgres.NewDatabaseAsync());

    public async Task<T> SendAsync<T>(IRequest<T> request)
    {
        await using var scope = _root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    public async Task<T> QueryAsync<T>(Func<StfrDbContext, Task<T>> query)
    {
        await using var scope = _root.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<StfrDbContext>());
    }

    /// <summary>The pages the sitemap would list, through the Storefront contract other modules use.</summary>
    public async Task<IReadOnlyList<Romp.Modules.Storefront.Contracts.PublishedPageRef>> ListSitemapPagesAsync()
    {
        await using var scope = _root.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Romp.Modules.Storefront.Contracts.IStorefrontPageQueries>()
            .ListPublishedPagesAsync(default);
    }

    public async Task<int> DraftVersionAsync() => (await SendAsync(new GetDraftSettingsQuery())).Version;

    /// <summary>Gives the two legal placeholders a description and publishes all eight pages, so the whole draft is publishable.</summary>
    public async Task PublishAllPagesAsync()
    {
        var pages = await SendAsync(new ListPagesQuery(PageSize: 100));
        foreach (var row in pages.Items)
        {
            var page = (await SendAsync(new GetPageQuery(row.Id)))!;
            if (page.MetaDescription.Length == 0)
            {
                page = await SendAsync(new UpdatePageCommand(page.Id, page.RowVersion, page.Slug, page.Title, $"{page.Title} information.", page.Blocks));
            }

            await SendAsync(new ChangePageStatusCommand(page.Id, page.RowVersion, PageStatus.Published));
        }
    }

    public void Dispose()
    {
        _root.Dispose();

        // One database per test: close its idle pooled connections so a long run never reaches the server's connection limit.
        using var connection = new NpgsqlConnection(ConnectionString);
        NpgsqlConnection.ClearPool(connection);
    }
}

internal sealed class FakeListingSettings : Romp.Modules.Storefront.Application.Listings.IListingSettings
{
    public int NewArrivalsDays => 30;
}

/// <summary>Catalog's path contract, answering what a test says; an unknown path is NotFound.</summary>
internal sealed class FakePathQueries : Romp.Modules.Catalog.Contracts.IProductPathQueries
{
    public Dictionary<string, Romp.Modules.Catalog.Contracts.PathResolutionResult> Answers { get; } = [];

    public Task<Romp.Modules.Catalog.Contracts.PathResolutionResult> ResolveAsync(string path, CancellationToken cancellationToken) =>
        Task.FromResult(Answers.GetValueOrDefault(path) ?? new(Romp.Modules.Catalog.Contracts.PathOutcome.NotFound, null, null));
}

internal sealed class FakeProductPageMetrics : Romp.Modules.Storefront.Application.ProductPages.IProductPageMetrics
{
    public int NotProjected { get; private set; }

    public void NotProjectedYet() => NotProjected++;
}
