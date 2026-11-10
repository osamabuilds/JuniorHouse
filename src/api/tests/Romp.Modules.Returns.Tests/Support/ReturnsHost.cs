using System.Data.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Returns.Application;
using Romp.Modules.Returns.Application.Abstractions;
using Romp.Modules.Returns.Application.ReverseQc;
using Romp.Modules.Returns.Domain.ReverseQc;
using Romp.Modules.Returns.Infrastructure.Persistence;
using Romp.Modules.Returns.Infrastructure.ReverseQc;
using Romp.Modules.Shipping.Contracts;
using Romp.Modules.Warehouse.Contracts;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Returns.Tests.Support;

/// <summary>One real PostgreSQL (Testcontainers, CLAUDE.md) with the RTRN migrations applied, shared by the tests of a collection.</summary>
public sealed class ReturnsPostgres : IAsyncLifetime
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

    internal RtrnDbContext NewContext()
    {
        var builder = new DbContextOptionsBuilder<RtrnDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "RTRN"));
        return new RtrnDbContext(builder.Options);
    }
}

[CollectionDefinition(Name)]
public sealed class ReturnsPostgresFixtureDefinition : ICollectionFixture<ReturnsPostgres>
{
    public const string Name = "Returns PostgreSQL";
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>What Orders would answer: a test sets exactly which orders exist and what they hold.</summary>
internal sealed class FakeOrders : IOrderFulfilmentQueries
{
    public Dictionary<long, OrderFulfilmentView> Views { get; } = [];

    public Task<OrderFulfilmentView?> FindAsync(long orderId, CancellationToken cancellationToken) => Task.FromResult(Views.TryGetValue(orderId, out var v) ? v : null);
}

/// <summary>What Warehouse would answer: only the listed bins exist; every accepted request is recorded, once per key.</summary>
internal sealed class FakeStockReturns : IStockReturns
{
    public HashSet<string> Bins { get; } = new(StringComparer.OrdinalIgnoreCase) { "A-01-01" };

    public Dictionary<string, RestockRequest> Posted { get; } = [];

    public int Calls { get; private set; }

    public Task<RestockResult> RestockAsync(RestockRequest request, CancellationToken cancellationToken)
    {
        Calls++;
        if (!Bins.Contains(request.BinCode))
        {
            return Task.FromResult(new RestockResult(false, $"Bin {request.BinCode.ToUpperInvariant()} does not exist."));
        }

        Posted.TryAdd(request.IdempotencyKey, request);
        return Task.FromResult(new RestockResult(true, null));
    }
}

internal sealed class CommandCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => _count;

    public void Reset() => _count = 0;

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

/// <summary>The same MediatR + FluentValidation pipeline <c>ReturnsModule</c> wires in production, against the real PostgreSQL, with a fake Orders and a fake Warehouse.</summary>
internal sealed class ReturnsHost : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    public const string Staff = "Ayesha Khan";

    private static long _orderSeed = 3_000_000;

    private readonly ServiceProvider _root;

    public ReturnsHost(ReturnsPostgres postgres, CommandCounter? counter = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();

        Time = new FixedTimeProvider(Start);
        services.AddSingleton<TimeProvider>(Time);
        Orders = new FakeOrders();
        Stock = new FakeStockReturns();
        services.AddSingleton<IOrderFulfilmentQueries>(Orders);
        services.AddSingleton<IStockReturns>(Stock);

        services.AddDbContext<RtrnDbContext>((sp, o) =>
        {
            o.UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "RTRN"));
            o.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<OutboxSaveChangesInterceptor>());
            if (counter is not null)
            {
                o.AddInterceptors(counter);
            }
        });
        services.AddScoped<IReturnsDbContext>(sp => sp.GetRequiredService<RtrnDbContext>());
        services.AddScoped<ShipmentReturnedConsumer>();
        services.AddSingleton(NullLoggerFactory.Instance);

        var assembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(assembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(ReturnsTransactionBehavior<,>));
        });

        _root = services.BuildServiceProvider();
    }

    public FixedTimeProvider Time { get; }

    public FakeOrders Orders { get; }

    public FakeStockReturns Stock { get; }

    /// <summary>An order that exists in Orders with the given lines (variant id, quantity); returns its id and number.</summary>
    public (long OrderId, string OrderNumber, long[] Variants) PlaceOrder(params int[] quantities)
    {
        var id = Interlocked.Increment(ref _orderSeed);
        var number = $"RMP-R{id}";
        var lines = quantities.Select((q, i) => new OrderFulfilmentLine(id * 10 + i, $"SKU-{id}-{i}", $"Frock {i}", "Red", "4-5Y", q, 1500m, 1500m * q)).ToList();
        Orders.Views[id] = new OrderFulfilmentView(id, number, $"key-{id}", "COD", true, "CNFD", "Sara Khan", "Lahore", 1500m, 0m, 200m, 1700m, 1700m, Start, lines);
        return (id, number, [.. lines.Select(l => l.VariantId)]);
    }

    /// <summary>Opens the case the way the consumer does and returns its id.</summary>
    public async Task<long> OpenCaseAsync(params int[] quantities)
    {
        var (orderId, number, _) = PlaceOrder(quantities.Length == 0 ? [2] : quantities);
        return (await SendAsync(new OpenReturnCaseCommand(orderId, number)))!.Value;
    }

    public async Task<ReturnCaseDetailDto> DetailAsync(long caseId) => (await SendAsync(new GetReturnCaseQuery(caseId)))!;

    public AsyncServiceScope Scope() => _root.CreateAsyncScope();

    /// <summary>Runs one request in its own scope (its own DbContext and transaction), like one HTTP request or one outbox message.</summary>
    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = Scope();
        return await action(scope.ServiceProvider);
    }

    public void Dispose() => _root.Dispose();
}
