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
using Romp.Modules.Labelling.Application;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.LabelWork;
using Romp.Modules.Labelling.Application.PrintJobs;
using Romp.Modules.Labelling.Application.Tags;
using Romp.Modules.Labelling.Application.Variants;
using Romp.Modules.Labelling.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Labelling.Tests.Support;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

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

/// <summary>The other modules as the label feature may see them (Catalog, Reference, Vendor): a test sets exactly what exists.</summary>
internal sealed class FakeLabelStyleLookup : ILabelStyleLookup
{
    public const long PoId = 7001;
    public const long StyleId = 500;

    public Dictionary<long, long?> StyleByPo { get; } = new() { [PoId] = StyleId };

    public Dictionary<long, LabelStyle> Styles { get; } = new()
    {
        [StyleId] = new LabelStyle(StyleId, "STY-DRESS", "Floral Summer Dress", 1299m, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), true),
    };

    public Dictionary<short, LabelCode> Colours { get; } = new()
    {
        [10] = new LabelCode(10, "RED", "Red"),
        [11] = new LabelCode(11, "BLUE", "Blue"),
    };

    public Dictionary<short, LabelCode> Sizes { get; } = new()
    {
        [1] = new LabelCode(1, "2-3Y", "2-3 Years"),
        [2] = new LabelCode(2, "4-5Y", "4-5 Years"),
        [3] = new LabelCode(3, "6-7Y", "6-7 Years"),
    };

    public Task<long?> FindStyleIdForPoAsync(long poId, CancellationToken cancellationToken) =>
        Task.FromResult(StyleByPo.TryGetValue(poId, out var id) ? id : null);

    public Task<IReadOnlyDictionary<long, LabelStyle>> GetStylesAsync(IReadOnlyCollection<long> styleIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<long, LabelStyle>>(Styles.Where(p => styleIds.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));

    public Task<IReadOnlyDictionary<short, LabelCode>> GetColoursAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<short, LabelCode>>(Colours.Where(p => ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));

    public Task<IReadOnlyDictionary<short, LabelCode>> GetSizesAsync(IReadOnlyCollection<short> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<short, LabelCode>>(Sizes.Where(p => ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value));
}

/// <summary>A printer a test controls: per printer code it succeeds or fails, and it remembers every document it was sent.</summary>
internal sealed class FakeLabelPrinter : ILabelPrinter
{
    private readonly object _gate = new();

    public HashSet<string> FailingPrinters { get; } = [];

    public List<(string PrinterCode, LabelDocument Document)> Sent { get; } = [];

    public Task<PrintOutcome> SendAsync(PrinterTarget printer, LabelDocument document, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (FailingPrinters.Contains(printer.Code))
            {
                return Task.FromResult(PrintOutcome.Fail("Paper jam"));
            }

            Sent.Add((printer.Code, document));
            return Task.FromResult(PrintOutcome.Ok($"out/{Sent.Count}"));
        }
    }
}

/// <summary>One real PostgreSQL (Testcontainers, CLAUDE.md) with the SKU migrations applied, shared by the tests of a collection.</summary>
public sealed class LabellingPostgres : IAsyncLifetime
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

    internal SkuDbContext NewContext(CommandCounter? counter = null)
    {
        var builder = new DbContextOptionsBuilder<SkuDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "SKU"));
        if (counter is not null)
        {
            builder.AddInterceptors(counter);
        }

        return new SkuDbContext(builder.Options);
    }
}

[CollectionDefinition(Name)]
public sealed class LabellingPostgresFixtureDefinition : ICollectionFixture<LabellingPostgres>
{
    public const string Name = "Labelling PostgreSQL";
}

/// <summary>
/// The same MediatR + FluentValidation pipeline <c>LabellingModule</c> wires in production, against the real PostgreSQL (the tests
/// here are about SQL: unique and partial unique indexes, row locks, atomic claims, the outbox). The other modules and the printer are fakes.
/// </summary>
internal sealed class LabellingHost : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _root;

    public LabellingHost(LabellingPostgres postgres, Action<LabellingOptions>? configureOptions = null, CommandCounter? counter = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();

        Time = new FixedTimeProvider(Start);
        services.AddSingleton<TimeProvider>(Time);

        Lookup = new FakeLabelStyleLookup();
        Printer = new FakeLabelPrinter();
        services.AddSingleton<ILabelStyleLookup>(Lookup);
        services.AddSingleton<ILabelPrinter>(Printer);

        Options = new LabellingOptions
        {
            CompanyPrefix = "0200000",
            AllowFakePrinters = true,
            BackoffSeconds = [10, 20, 30],
            MaxAttempts = 3,
            StuckJobMinutes = 5,
        };
        configureOptions?.Invoke(Options);
        services.AddSingleton<IOptions<LabellingOptions>>(Microsoft.Extensions.Options.Options.Create(Options));

        services.AddDbContext<SkuDbContext>((sp, options) =>
        {
            options.UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "SKU"));
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<OutboxSaveChangesInterceptor>());
            if (counter is not null)
            {
                options.AddInterceptors(counter);
            }
        });
        services.AddScoped<ILabellingDbContext>(sp => sp.GetRequiredService<SkuDbContext>());
        services.AddScoped<IGtinSequence, GtinSequence>();
        services.AddScoped<IJobNumberAllocator, JobNumberAllocator>();
        services.AddScoped<IRowLocks, RowLocks>();
        services.AddScoped<IPrintJobClaims, PrintJobClaims>();
        services.AddScoped<IVariantAssigner, VariantAssigner>();
        services.AddScoped<IVariantReader, VariantReader>();
        services.AddScoped<IPrintProgress, PrintProgress>();
        services.AddScoped<ITagDocumentBuilder, TagDocumentBuilder>();
        services.AddScoped<LabelWorkResolver>();
        services.AddScoped<PrintJobProcessor>();
        services.AddSingleton(NullLoggerFactory.Instance);

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LabellingTransactionBehavior<,>));
        });

        _root = services.BuildServiceProvider();
    }

    public FixedTimeProvider Time { get; }

    public FakeLabelStyleLookup Lookup { get; }

    public FakeLabelPrinter Printer { get; }

    public LabellingOptions Options { get; }

    private static long _styleSeed = 10_000;

    /// <summary>A fresh style and PO in the fake Catalog/Vendor, so a test owns its variants even though the database is shared.</summary>
    public (long PoId, long StyleId) NewStyle(string? code = null, bool active = true, decimal price = 1299m)
    {
        var id = Interlocked.Increment(ref _styleSeed);
        Lookup.Styles[id] = new LabelStyle(id, code ?? $"STY-{id}", $"Style {id}", price, Start, active);
        Lookup.StyleByPo[id] = id;
        return (id, id);
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

    public void Dispose() => _root.Dispose();
}

/// <summary>Helpers that build the QC message shape the tests feed in.</summary>
internal static class Batches
{
    private static long _next = 1_000;

    public static MappedBatch Accepted(params (short Size, short Colour, int Qty)[] lines) =>
        Batch(Romp.Modules.Labelling.Domain.LabelOutcome.Accepted, FakeLabelStyleLookup.PoId, lines);

    public static MappedBatch Concession(params (short Size, short Colour, int Qty)[] lines) =>
        Batch(Romp.Modules.Labelling.Domain.LabelOutcome.Concession, FakeLabelStyleLookup.PoId, lines);

    public static MappedBatch Batch(short outcome, long poId, params (short Size, short Colour, int Qty)[] lines) =>
        new(Interlocked.Increment(ref _next), Interlocked.Increment(ref _next), poId, outcome,
            lines.Select(l => new MappedLine(l.Size, l.Colour, l.Qty)).ToList());
}
