using System.Data.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Warehouse.Application;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Application.Bins;
using Romp.Modules.Warehouse.Infrastructure.Bins;
using Romp.Modules.Warehouse.Infrastructure.Persistence;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Infrastructure.StockLedger;
using Romp.Modules.Warehouse.Infrastructure.Putaway;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Warehouse.Tests.Support;

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

/// <summary>What the stock ledger would answer about bins: a test sets exactly what each bin holds.</summary>
internal sealed class FakeBinStockQuery : IBinStockQuery
{
    public Dictionary<long, int> Units { get; } = [];

    public Task<IReadOnlyDictionary<long, int>> GetUnitsOnHandAsync(IReadOnlyCollection<long> binIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<long, int>>(Units.Where(p => binIds.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));
}

/// <summary>One real PostgreSQL (Testcontainers, CLAUDE.md) with the WHSE migrations applied, shared by the tests of a collection.</summary>
public sealed class WarehousePostgres : IAsyncLifetime
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

    internal WhseDbContext NewContext(CommandCounter? counter = null)
    {
        var builder = new DbContextOptionsBuilder<WhseDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "WHSE"));
        if (counter is not null)
        {
            builder.AddInterceptors(counter);
        }

        return new WhseDbContext(builder.Options);
    }
}

[CollectionDefinition(Name)]
public sealed class WarehousePostgresFixtureDefinition : ICollectionFixture<WarehousePostgres>
{
    public const string Name = "Warehouse PostgreSQL";
}

/// <summary>
/// The same MediatR + FluentValidation pipeline <c>WarehouseModule</c> wires in production, against the real PostgreSQL (the tests here
/// are about SQL: unique indexes, check constraints, row locks, the outbox). Other features' ports are fakes until those features land.
/// </summary>
internal sealed class WarehouseHost : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static long _zoneSeed;

    private readonly ServiceProvider _root;

    public WarehouseHost(WarehousePostgres postgres, Action<WarehouseOptions>? configureOptions = null, CommandCounter? counter = null, bool realBinStock = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();

        Time = new FixedTimeProvider(Start);
        services.AddSingleton<TimeProvider>(Time);

        BinStock = new FakeBinStockQuery();
        if (realBinStock)
        {
            services.AddScoped<IBinStockQuery, BinStockQuery>();
        }
        else
        {
            services.AddSingleton<IBinStockQuery>(BinStock);
        }

        Options = new WarehouseOptions();
        configureOptions?.Invoke(Options);
        services.AddSingleton<IOptions<WarehouseOptions>>(Microsoft.Extensions.Options.Options.Create(Options));

        services.AddDbContext<WhseDbContext>((sp, options) =>
        {
            options.UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "WHSE"));
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<OutboxSaveChangesInterceptor>());
            if (counter is not null)
            {
                options.AddInterceptors(counter);
            }
        });
        services.AddScoped<IWarehouseDbContext>(sp => sp.GetRequiredService<WhseDbContext>());
        services.AddScoped<IBinReader, BinReader>();
        services.AddScoped<IBinCountMarker, BinCountMarker>();
        services.AddScoped<IStockPosting, StockPosting>();
        services.AddScoped<Romp.Modules.Warehouse.Application.StockAlerts.AlertEvaluator>();
        services.AddScoped<Romp.Modules.Warehouse.Application.CycleCounts.ICountNumberAllocator, Romp.Modules.Warehouse.Infrastructure.CycleCounts.CountNumberAllocator>();
        services.AddScoped<Romp.Modules.Warehouse.Application.Adjustments.IAdjustmentPoster, Romp.Modules.Warehouse.Application.Adjustments.AdjustmentPoster>();
        services.AddScoped<IStockVariantReader, StockVariantReader>();
        PoStyles = new FakePoStyleLookup();
        services.AddSingleton<Romp.Modules.Warehouse.Application.Putaway.IPoStyleLookup>(PoStyles);
        services.AddScoped<Romp.Modules.Warehouse.Application.Putaway.IPutawayRowLocks, PutawayRowLocks>();
        services.AddSingleton(NullLoggerFactory.Instance);

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.RegisterServicesFromAssembly(typeof(TestPostCommand).Assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(WarehouseTransactionBehavior<,>));
        });

        _root = services.BuildServiceProvider();
    }

    public FixedTimeProvider Time { get; }

    public FakeBinStockQuery BinStock { get; }

    public FakePoStyleLookup PoStyles { get; }

    public WarehouseOptions Options { get; }

    /// <summary>A zone no other test uses, so tests own their bins even though the database is shared.</summary>
    public static string NewZone()
    {
        const string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var n = Interlocked.Increment(ref _zoneSeed);
        var chars = new char[3];
        for (var i = 2; i >= 0; i--)
        {
            chars[i] = digits[(int)(n % 36)];
            n /= 36;
        }

        return new string(chars);
    }

    private static long _variantSeed = 1_000;

    /// <summary>A fresh Labelling variant as warehouse sees it (unique id, SKU and GTIN), recorded through the real event command.</summary>
    public async Task<(long RefId, long VariantId, string Gtin, string Sku)> NewVariantAsync()
    {
        var n = Interlocked.Increment(ref _variantSeed);
        var gtin = Romp.Modules.Warehouse.Domain.Gtin13.WithCheckDigit("2" + n.ToString("00000000000", System.Globalization.CultureInfo.InvariantCulture));
        var sku = $"ROMP-TEST-{n}";
        var refId = await SendAsync(new RecordVariantAssignedCommand(n, 500 + n, 10, 1, sku, gtin));
        return (refId, n, gtin, sku);
    }

    public async Task<BinRowDto> NewBinAsync(string? zone = null) =>
        await SendAsync(new CreateBinCommand(zone ?? NewZone(), "01", "A", "Test Person"));

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

    public void Dispose() => _root.Dispose();
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Test-only command that runs <see cref="IStockPosting"/> inside the module's transaction behaviour, like a real feature command would.</summary>
internal sealed record TestPostCommand(IReadOnlyList<Romp.Modules.Warehouse.Domain.StockLedger.StockPost> Posts, string Person = "Test Person")
    : IRequest<IReadOnlyList<PostedMovement>>, IWarehouseCommand;

internal sealed class TestPostCommandHandler(IStockPosting posting) : IRequestHandler<TestPostCommand, IReadOnlyList<PostedMovement>>
{
    public Task<IReadOnlyList<PostedMovement>> Handle(TestPostCommand request, CancellationToken cancellationToken) =>
        posting.PostAsync(request.Posts, request.Person, cancellationToken);
}

/// <summary>Vendor as putaway may see it: a test sets exactly which PO belongs to which style.</summary>
internal sealed class FakePoStyleLookup : Romp.Modules.Warehouse.Application.Putaway.IPoStyleLookup
{
    public Dictionary<long, long> StyleByPo { get; } = [];

    public Task<long?> FindStyleIdForPoAsync(long poId, CancellationToken cancellationToken) =>
        Task.FromResult(StyleByPo.TryGetValue(poId, out var id) ? (long?)id : null);
}
