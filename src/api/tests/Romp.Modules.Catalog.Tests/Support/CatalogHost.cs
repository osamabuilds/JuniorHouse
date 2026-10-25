using System.Data.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.BuildingBlocks.Storage;
using Romp.Modules.Catalog.Application;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Application.CompositionCare;
using Romp.Modules.Catalog.Application.ProductMedia;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Application.SizeCharts;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Catalog.Infrastructure.Products;
using Romp.Modules.Catalog.Infrastructure.Urls;
using Romp.Modules.Labelling.Contracts;
using Romp.Modules.Reference.Contracts;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Catalog.Tests.Support;

/// <summary>Counts the SQL reader commands a context runs, to prove a list is a constant number of queries (no N+1, CONVENTIONS section 4).</summary>
internal sealed class CommandCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => _count;

    public void Reset() => _count = 0;

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

/// <summary>
/// Reference data as the Catalog contract would answer it: a test can add or change rows. The defaults mirror the seed (genders, a few
/// categories, age brackets, collections, fibres, care instructions, colours and sizes).
/// </summary>
internal sealed class FakeLookups : ICatalogLookupQueries
{
    public Dictionary<short, ApparelCode> Genders { get; } = new()
    {
        [1] = new(1, "BOYS", "Boys"), [2] = new(2, "GIRLS", "Girls"), [3] = new(3, "UNISEX", "Unisex"),
    };

    public Dictionary<short, ApparelCode> Categories { get; } = new()
    {
        [1] = new(1, "TOPS", "Tops"), [2] = new(2, "BOTTOMS", "Bottoms"), [3] = new(3, "DRESSES", "Dresses"),
    };

    public Dictionary<short, ApparelCode> AgeBrackets { get; } = new()
    {
        [3] = new(3, "TODDLER", "Toddler"), [4] = new(4, "KIDS", "Kids"), [5] = new(5, "TWEEN", "Tween"),
    };

    public Dictionary<short, ApparelCode> Collections { get; } = new() { [1] = new(1, "NEW-ARRIVALS", "New Arrivals"), [2] = new(2, "SALE", "Sale") };

    public Dictionary<short, ApparelCode> Fibres { get; } = new()
    {
        [1] = new(1, "COTTON", "Cotton"), [2] = new(2, "POLYESTER", "Polyester"), [3] = new(3, "ELASTANE", "Elastane"),
    };

    public Dictionary<short, CareInstructionCode> Care { get; } = new()
    {
        [1] = new(1, "WASH-30", "Machine wash at 30 C", "wash-30"), [2] = new(2, "NO-BLEACH", "Do not bleach", "no-bleach"),
    };

    public Dictionary<short, ApparelCode> Colours { get; } = new()
    {
        [1] = new(1, "RED", "Red"), [2] = new(2, "BLU", "Blue"), [3] = new(3, "GRN", "Green"),
    };

    public Dictionary<short, ApparelCode> Sizes { get; } = new()
    {
        [4] = new(4, "1-2Y", "1-2 Years"), [5] = new(5, "2-3Y", "2-3 Years"), [6] = new(6, "3-4Y", "3-4 Years"),
    };

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetGendersAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => Pick(Genders, ids);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetCategoriesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => Pick(Categories, ids);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetAgeBracketsAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => Pick(AgeBrackets, ids);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetCollectionsAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => Pick(Collections, ids);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetFibresAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => Pick(Fibres, ids);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetSizesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => Pick(Sizes, ids);

    public Task<IReadOnlyDictionary<short, ApparelCode>> GetColoursAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) => Pick(Colours, ids);

    public Task<IReadOnlyDictionary<short, CareInstructionCode>> GetCareInstructionsAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<short, CareInstructionCode>>(Care.Where(p => ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));

    public Task<IReadOnlyDictionary<string, short>> GetGenderIdsBySegmentAsync(IReadOnlyCollection<string> segments, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, short>>(Genders.Values.Where(g => segments.Contains(g.Code.ToLowerInvariant())).ToDictionary(g => g.Code.ToLowerInvariant(), g => g.Id));

    public Task<IReadOnlyDictionary<string, short>> GetCategoryIdsBySegmentAsync(IReadOnlyCollection<string> segments, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, short>>(Categories.Values.Where(c => segments.Contains(c.Code.ToLowerInvariant())).ToDictionary(c => c.Code.ToLowerInvariant(), c => c.Id));

    public Task<IReadOnlyList<CatalogLookupItem>> ListGendersAsync(CancellationToken cancellationToken) => Items(Genders);

    public Task<IReadOnlyList<CatalogLookupItem>> ListCategoriesAsync(CancellationToken cancellationToken) => Items(Categories);

    public Task<IReadOnlyList<CatalogLookupItem>> ListAgeBracketsAsync(CancellationToken cancellationToken) => Items(AgeBrackets);

    public Task<IReadOnlyList<CatalogLookupItem>> ListCollectionsAsync(CancellationToken cancellationToken) => Items(Collections);

    public Task<IReadOnlyList<CatalogLookupItem>> ListSizesAsync(CancellationToken cancellationToken) => Items(Sizes);

    public Task<IReadOnlyList<CatalogLookupItem>> ListColoursAsync(CancellationToken cancellationToken) => Items(Colours);

    private static Task<IReadOnlyList<CatalogLookupItem>> Items(Dictionary<short, ApparelCode> source) =>
        Task.FromResult<IReadOnlyList<CatalogLookupItem>>(source.Values.OrderBy(c => c.Id).Select(c => new CatalogLookupItem(c.Id, c.Code, c.Name, c.Id, true)).ToList());

    private static Task<IReadOnlyDictionary<short, ApparelCode>> Pick(Dictionary<short, ApparelCode> source, IReadOnlyCollection<short> ids) =>
        Task.FromResult<IReadOnlyDictionary<short, ApparelCode>>(source.Where(p => ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));
}

/// <summary>Labelling as Catalog sees it through its contract: a test sets exactly which variants exist and what GTIN state they are in.</summary>
internal sealed class FakeVariantQueries : IVariantQueries
{
    public List<LabelledVariantInfo> Variants { get; } = [];

    public int CallCount { get; private set; }

    public Task<IReadOnlyList<LabelledVariantInfo>> GetByStyleAsync(long styleId, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult<IReadOnlyList<LabelledVariantInfo>>(Variants.Where(v => v.StyleId == styleId).ToList());
    }

    public Task<LabelledVariantInfo?> GetByIdAsync(long variantId, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(Variants.FirstOrDefault(v => v.VariantId == variantId));
    }
}

/// <summary>One real PostgreSQL (Testcontainers, CLAUDE.md) with the CTLG migrations applied, shared by the tests of a collection.</summary>
public sealed class CatalogPostgres : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync().AsTask();

    internal CatalogDbContext NewContext(CommandCounter? counter = null)
    {
        var builder = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CTLG"));
        if (counter is not null)
        {
            builder.AddInterceptors(counter);
        }

        return new CatalogDbContext(builder.Options);
    }
}

[CollectionDefinition(Name)]
public sealed class CatalogPostgresFixtureDefinition : ICollectionFixture<CatalogPostgres>
{
    public const string Name = "Catalog PostgreSQL";
}

/// <summary>
/// The same MediatR + FluentValidation pipeline <c>CatalogModule</c> wires in production, against the real PostgreSQL (the tests here
/// are about SQL: unique indexes, check constraints, triggers, the outbox, the concurrency token). Reference and Labelling are fakes
/// behind their contracts; file storage is in memory.
/// </summary>
internal sealed class CatalogHost : IDisposable
{
    private static long _styleSeed = 20_000;
    private static long _variantSeed = 40_000;

    private readonly ServiceProvider _root;

    public CatalogHost(CatalogPostgres postgres, Action<CatalogOptions>? configureOptions = null, CommandCounter? counter = null)
    {
        Postgres = postgres;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();

        Lookups = new FakeLookups();
        Labelling = new FakeVariantQueries();
        Storage = new InMemoryFileStorage();
        services.AddSingleton<ICatalogLookupQueries>(Lookups);
        services.AddSingleton<IVariantQueries>(Labelling);
        services.AddSingleton<IFileStorage>(Storage);

        Options = new CatalogOptions();
        configureOptions?.Invoke(Options);
        services.AddSingleton<IOptions<CatalogOptions>>(Microsoft.Extensions.Options.Options.Create(Options));
        services.AddSingleton(Options);

        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            options.UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CTLG"));
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<OutboxSaveChangesInterceptor>());
            if (counter is not null)
            {
                options.AddInterceptors(counter);
            }
        });
        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());
        services.AddScoped<IProductPathBuilder, ProductPathBuilder>();
        services.AddScoped<ProductPathWriter>();
        services.AddScoped<PathResolver>();
        services.AddScoped<IProductPathQueries, ProductPathQueries>();
        services.AddScoped<IMediaGate, MediaGate>();
        services.AddScoped<ICompositionGate, CompositionGate>();
        services.AddScoped<IChartGate, ChartGate>();
        services.AddScoped<ProductAssembler>();
        services.AddScoped<IPublishedProductQueries, PublishedProductQueries>();
        services.AddScoped<ISizeChartQueries, SizeChartQueries>();
        services.AddScoped<LabelledVariantWriter>();
        services.AddScoped<ProductImageMapper>();
        services.AddScoped<CompositionCareReader>();
        services.AddScoped<SizeChartMapper>();
        services.AddScoped<PendingFileDeletes>();

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(CatalogTransactionBehavior<,>));
        });

        services.AddScoped<VariantGtinAssignedConsumer>();
        services.AddScoped<GtinVoidedConsumer>();

        _root = services.BuildServiceProvider();
    }

    public CatalogPostgres Postgres { get; }

    public FakeLookups Lookups { get; }

    public FakeVariantQueries Labelling { get; }

    public InMemoryFileStorage Storage { get; }

    public CatalogOptions Options { get; }

    /// <summary>
    /// A fresh style with the given colours and sizes (default: 2 colours × 3 sizes = 6 variants) and its own gender/category free
    /// of other tests' slugs is not needed: slugs come from unique names, so tests own their products on a shared database.
    /// </summary>
    public async Task<long> NewStyleAsync(short[]? colours = null, short[]? sizes = null, decimal retailPrice = 1299m, bool active = true)
    {
        var id = Interlocked.Increment(ref _styleSeed);
        await using var db = Postgres.NewContext();
        var style = new Style($"STY-{id}", $"Style {id}", null, 3, 2, 4, 1, 450m, retailPrice);
        db.Styles.Add(style);
        await db.SaveChangesAsync();
        style.SetColourSizeAndTargets(colours ?? [1, 2], sizes ?? [4, 5, 6], []);
        if (!active)
        {
            style.Retire();
        }

        await db.SaveChangesAsync();
        return style.Id;
    }

    /// <summary>Tells the fake Labelling that a variant of a style received its GTIN (or lost it).</summary>
    public LabelledVariantInfo Label(long styleId, short colourId, short sizeId, bool voided = false)
    {
        var id = Interlocked.Increment(ref _variantSeed);
        var info = new LabelledVariantInfo(id, styleId, colourId, sizeId, $"ROMP-S{styleId}-{colourId}-{sizeId}", id.ToString("0000000000000", System.Globalization.CultureInfo.InvariantCulture), voided);
        Labelling.Variants.Add(info);
        return info;
    }

    /// <summary>A unique product name, so slugs never collide across tests that share the database.</summary>
    public static string UniqueName(string stem = "Floral Dress") => $"{stem} {Guid.NewGuid():N}"[..Math.Min(60, stem.Length + 9)];

    public async Task<ProductDto> NewProductAsync(long? styleId = null, string? name = null, short genderId = 2, short categoryId = 3)
    {
        var style = styleId ?? await NewStyleAsync();
        return await SendAsync(new CreateProductCommand(style, name ?? UniqueName(), "A soft cotton dress for summer days.", genderId, categoryId, 4, "Test Person"));
    }

    public AsyncServiceScope Scope() => _root.CreateAsyncScope();

    /// <summary>Runs one request in its own scope (its own DbContext and transaction), like one HTTP request or one outbox message.</summary>
    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task SendAsync(IRequest request)
    {
        await using var scope = Scope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Scope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>The product made ready to publish: image with alt text, composition 100%, one care instruction, prices on its labelled variants.</summary>
    public async Task<ProductDto> MakePublishableAsync(ProductDto product, params (short Colour, short Size)[] labelled)
    {
        foreach (var (colour, size) in labelled)
        {
            Label(product.StyleId, colour, size);
        }

        product = await SendAsync(new RefreshVariantsFromLabellingCommand(product.Id));
        await SendAsync(new UploadProductImageCommand(product.Id, TestImages.Png(40, 60), "front.png", null, "A red floral summer dress, front view", "Test Person"));
        var current = await SendAsync(new GetProductQuery(product.Id));
        await SendAsync(new SetCompositionCommand(product.Id, [new CompositionRowInput(1, 95m), new CompositionRowInput(3, 5m)], current.Version, "Test Person"));
        current = await SendAsync(new GetProductQuery(product.Id));
        await SendAsync(new SetCareCommand(product.Id, [1, 2], null, current.Version, "Test Person"));
        return await SendAsync(new GetProductQuery(product.Id));
    }

    public void Dispose() => _root.Dispose();
}

/// <summary>Image bytes built in the test (no binary fixtures): a PNG header with the given dimensions and a WebP and JPEG too.</summary>
internal static class TestImages
{
    public static byte[] Png(int width, int height)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 };
        bytes.AddRange("IHDR"u8.ToArray());
        bytes.AddRange(BigEndian(width));
        bytes.AddRange(BigEndian(height));
        bytes.AddRange([8, 2, 0, 0, 0, 0, 0, 0, 0]);
        return [.. bytes];
    }

    public static byte[] Jpeg(int width, int height) =>
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x4A, 0x46,
        0xFF, 0xC0, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x03, 0x01, 0x22, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01,
        0xFF, 0xD9,
    ];

    /// <summary>A lossless WebP (VP8L) header.</summary>
    public static byte[] WebpLossless(int width, int height)
    {
        var bits = (uint)(width - 1) | ((uint)(height - 1) << 14);
        return
        [
            .. "RIFF"u8.ToArray(), 22, 0, 0, 0, .. "WEBP"u8.ToArray(), .. "VP8L"u8.ToArray(), 10, 0, 0, 0, 0x2F,
            (byte)bits, (byte)(bits >> 8), (byte)(bits >> 16), (byte)(bits >> 24), 0, 0, 0, 0, 0, 0,
        ];
    }

    private static byte[] BigEndian(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
