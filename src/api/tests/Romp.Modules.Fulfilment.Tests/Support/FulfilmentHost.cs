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
using Romp.Modules.Fulfilment.Application;
using Romp.Modules.Fulfilment.Application.Abstractions;
using Romp.Modules.Fulfilment.Application.PackStation;
using Romp.Modules.Fulfilment.Application.PackingSlips;
using Romp.Modules.Fulfilment.Infrastructure.Persistence;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Storefront.Contracts;
using Romp.Modules.Warehouse.Contracts;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Fulfilment.Tests.Support;

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

/// <summary>One real PostgreSQL (Testcontainers, CLAUDE.md) with the FULF migrations applied, shared by the tests of a collection.</summary>
public sealed class FulfilmentPostgres : IAsyncLifetime
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

    internal FulfDbContext NewContext(CommandCounter? counter = null)
    {
        var builder = new DbContextOptionsBuilder<FulfDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "FULF"));
        if (counter is not null)
        {
            builder.AddInterceptors(counter);
        }

        return new FulfDbContext(builder.Options);
    }
}

[CollectionDefinition(Name)]
public sealed class FulfilmentPostgresFixtureDefinition : ICollectionFixture<FulfilmentPostgres>
{
    public const string Name = "Fulfilment PostgreSQL";
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>What Warehouse would answer: a test sets which bins hold how many units of a variant. Allocation is first fit in the order given; it records every call.</summary>
internal sealed class FakeStockAllocator : IStockAllocator
{
    public Dictionary<long, (string Gtin, string Sku, List<(long BinId, string BinCode, string WalkKey, int Quantity)> Bins)> Stock { get; } = [];

    public Dictionary<string, List<AllocatedUnit>> Claims { get; } = [];

    public List<string> Released { get; } = [];

    public List<(string Key, IReadOnlyList<PickedUnit> Picks, string Staff)> Confirmed { get; } = [];

    public List<(string Key, long VariantId, long BinId, int Quantity)> Shortened { get; } = [];

    public List<long> Flagged { get; } = [];

    public List<long> Cleared { get; } = [];

    public int AllocateCalls { get; private set; }

    public void Stocked(long variantId, string gtin, params (long BinId, string BinCode, string WalkKey, int Quantity)[] bins) =>
        Stock[variantId] = (gtin, $"SKU-{variantId}", [.. bins]);

    public Task<AllocationResult> AllocateAsync(string orderKey, IReadOnlyList<AllocationLineRequest> lines, CancellationToken cancellationToken)
    {
        AllocateCalls++;
        var claims = Claims.TryGetValue(orderKey, out var existing) ? existing : Claims[orderKey] = [];
        var shortages = new List<ShortAllocation>();
        foreach (var line in lines)
        {
            var have = claims.Where(c => c.VariantId == line.VariantId).Sum(c => c.Quantity);
            var need = line.Quantity - have;
            if (Stock.TryGetValue(line.VariantId, out var stock))
            {
                foreach (var bin in stock.Bins.Where(b => !Flagged.Contains(b.BinId) || Cleared.Contains(b.BinId)))
                {
                    var taken = claims.Where(c => c.BinId == bin.BinId && c.VariantId == line.VariantId).Sum(c => c.Quantity);
                    var free = bin.Quantity - taken - Claims.Where(p => p.Key != orderKey).SelectMany(p => p.Value).Where(c => c.BinId == bin.BinId && c.VariantId == line.VariantId).Sum(c => c.Quantity);
                    var take = Math.Min(need, Math.Max(0, free));
                    if (take > 0)
                    {
                        var index = claims.FindIndex(c => c.BinId == bin.BinId && c.VariantId == line.VariantId);
                        if (index >= 0)
                        {
                            claims[index] = claims[index] with { Quantity = claims[index].Quantity + take };
                        }
                        else
                        {
                            claims.Add(new AllocatedUnit(line.VariantId, bin.BinId, bin.BinCode, bin.WalkKey, take, stock.Gtin, stock.Sku));
                        }

                        need -= take;
                    }
                }
            }

            if (need > 0)
            {
                shortages.Add(new ShortAllocation(line.VariantId, line.Quantity, line.Quantity - need));
            }
        }

        var wanted = lines.Select(l => l.VariantId).ToHashSet();
        return Task.FromResult(new AllocationResult([.. claims.Where(c => wanted.Contains(c.VariantId))], shortages));
    }

    public Task ReleaseAsync(string orderKey, CancellationToken cancellationToken)
    {
        Released.Add(orderKey);
        Claims.Remove(orderKey);
        return Task.CompletedTask;
    }

    public Task ConfirmPickAsync(string orderKey, IReadOnlyList<PickedUnit> picks, string staff, CancellationToken cancellationToken)
    {
        if (Confirmed.All(c => c.Key != orderKey))
        {
            Confirmed.Add((orderKey, picks, staff));
        }

        return Task.CompletedTask;
    }

    public Task ShortenClaimAsync(string orderKey, long variantId, long binId, int quantity, CancellationToken cancellationToken)
    {
        Shortened.Add((orderKey, variantId, binId, quantity));
        if (Claims.TryGetValue(orderKey, out var claims))
        {
            var index = claims.FindIndex(c => c.VariantId == variantId && c.BinId == binId);
            if (index >= 0)
            {
                var left = claims[index].Quantity - quantity;
                if (left <= 0)
                {
                    claims.RemoveAt(index);
                }
                else
                {
                    claims[index] = claims[index] with { Quantity = left };
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task FlagBinAsync(long binId, string reasonCode, CancellationToken cancellationToken)
    {
        Flagged.Add(binId);
        return Task.CompletedTask;
    }

    public Task ClearBinFlagAsync(long binId, CancellationToken cancellationToken)
    {
        Cleared.Add(binId);
        return Task.CompletedTask;
    }
}

/// <summary>What Orders would answer for the fulfilment queries.</summary>
internal sealed class FakeOrders : IOrderFulfilmentQueries
{
    public Dictionary<long, OrderFulfilmentView> Views { get; } = [];

    public Task<OrderFulfilmentView?> FindAsync(long orderId, CancellationToken cancellationToken) =>
        Task.FromResult(Views.TryGetValue(orderId, out var view) ? view : null);
}

internal sealed class FakeShopSettings : IShopSettingsQueries
{
    public ShopSlipSettings? Settings { get; set; } = new("Romp", "923001234567", "hello@romp.pk", "Mon to Sat, 10am to 6pm", 14);

    public Task<ShopSlipSettings?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Settings);
}

internal sealed class FakeSlipPrinter : ISlipPrinter
{
    public bool Up { get; set; } = true;

    public List<PackingSlipDto> Printed { get; } = [];

    public Task<bool> PrintAsync(PackingSlipDto slip, CancellationToken cancellationToken)
    {
        if (!Up)
        {
            return Task.FromResult(false);
        }

        Printed.Add(slip);
        return Task.FromResult(true);
    }
}

/// <summary>
/// The same MediatR + FluentValidation pipeline <c>FulfilmentModule</c> wires in production, against the real PostgreSQL (the tests here are about
/// SQL: unique indexes, check constraints, row versions, the outbox). Warehouse, Orders and the printer are fakes; the real Warehouse allocator is
/// tested in the Warehouse tests.
/// </summary>
internal sealed class FulfilmentHost : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    private static long _orderSeed = 1_000_000;
    private static long _variantSeed = 5_000;

    private readonly ServiceProvider _root;

    public FulfilmentHost(FulfilmentPostgres postgres, Action<FulfilmentOptions>? configureOptions = null, CommandCounter? counter = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();

        Time = new FixedTimeProvider(Start);
        services.AddSingleton<TimeProvider>(Time);

        Options = new FulfilmentOptions();
        configureOptions?.Invoke(Options);
        services.AddSingleton<IOptions<FulfilmentOptions>>(Microsoft.Extensions.Options.Options.Create(Options));

        Allocator = new FakeStockAllocator();
        Orders = new FakeOrders();
        Shop = new FakeShopSettings();
        Printer = new FakeSlipPrinter();
        services.AddSingleton<IStockAllocator>(Allocator);
        services.AddSingleton<IOrderFulfilmentQueries>(Orders);
        services.AddSingleton<IShopSettingsQueries>(Shop);
        services.AddSingleton<ISlipPrinter>(Printer);

        services.AddDbContext<FulfDbContext>((sp, options) =>
        {
            options.UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "FULF"));
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<OutboxSaveChangesInterceptor>());
            if (counter is not null)
            {
                options.AddInterceptors(counter);
            }
        });
        services.AddScoped<IFulfilmentDbContext>(sp => sp.GetRequiredService<FulfDbContext>());
        services.AddScoped<PackPlanner>();
        services.AddScoped<PackingSlipBuilder>();
        services.AddSingleton(NullLoggerFactory.Instance);

        var applicationAssembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(FulfilmentTransactionBehavior<,>));
        });

        _root = services.BuildServiceProvider();
    }

    public FixedTimeProvider Time { get; }

    public FulfilmentOptions Options { get; }

    public FakeStockAllocator Allocator { get; }

    public FakeOrders Orders { get; }

    public FakeShopSettings Shop { get; }

    public FakeSlipPrinter Printer { get; }

    public const string Picker = "Picker One";
    public const string OtherPicker = "Picker Two";
    public const string Packer = "Packer One";

    /// <summary>
    /// Puts a confirmed order with its lines into Orders' fake and stocks each variant in one bin, so the order can be created with
    /// <see cref="Seed.CreateAsync"/>. Lines are (quantity, bin code, bin id); every variant is new.
    /// </summary>
    public (long OrderId, string OrderNumber, long[] VariantIds, string[] Gtins) PlaceOrder(bool cod = true, params (int Quantity, string BinCode, int OnShelf)[] lines)
    {
        var orderId = Interlocked.Increment(ref _orderSeed);
        var number = $"RMP-{orderId}";
        var variants = new List<long>();
        var gtins = new List<string>();
        var viewLines = new List<OrderFulfilmentLine>();
        foreach (var (quantity, binCode, onShelf) in lines)
        {
            var variantId = Interlocked.Increment(ref _variantSeed);
            var gtin = Gtin(variantId);
            variants.Add(variantId);
            gtins.Add(gtin);
            Allocator.Stocked(variantId, gtin, (variantId * 10, binCode, WalkKey(binCode), onShelf));
            viewLines.Add(new OrderFulfilmentLine(variantId, $"SKU-{variantId}", "Floral Dress", "Red", "2-3Y", quantity, 1500m, 1500m * quantity));
        }

        var subtotal = viewLines.Sum(l => l.LineTotal);
        Orders.Views[orderId] = new OrderFulfilmentView(
            orderId, number, $"key-{orderId}", cod ? "COD" : "CARD", cod, "CNFD", "Sara Khan", "Lahore", subtotal, 0m, 200m, subtotal + 200m, cod ? subtotal + 200m : 0m, Start, viewLines);
        return (orderId, number, [.. variants], [.. gtins]);
    }

    public static string Gtin(long seed) => "2" + seed.ToString("00000000000", System.Globalization.CultureInfo.InvariantCulture) + "0";

    public static string WalkKey(string binCode)
    {
        var parts = binCode.Split('-');
        return $"{parts[0].PadRight(3)}-{parts[1].PadLeft(3, '0')}-{parts[2].PadRight(2)}";
    }

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

internal static class Seed
{
    /// <summary>Creates the fulfilment order for a placed order and returns its fulfilment id.</summary>
    public static async Task<long> CreateAsync(FulfilmentHost host, long orderId, FulfilmentPostgres postgres)
    {
        await host.SendAsync(new Romp.Modules.Fulfilment.Application.Allocations.CreateFulfilmentOrderCommand(orderId));
        await using var db = postgres.NewContext();
        return await db.Orders.Where(o => o.OrderId == orderId).Select(o => o.Id).SingleAsync();
    }
}
