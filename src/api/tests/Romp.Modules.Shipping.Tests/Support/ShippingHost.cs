using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Application.Captcha;
using Romp.BuildingBlocks.Persistence;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Shipping.Application;
using Romp.Modules.Shipping.Application.Abstractions;
using Romp.Modules.Shipping.Application.Bookings;
using Romp.Modules.Shipping.Application.CourierPort;
using Romp.Modules.Shipping.Application.Tracking;
using Romp.Modules.Shipping.Contracts;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Domain.Bookings;
using Romp.Modules.Shipping.Infrastructure.CourierPort;
using Romp.Modules.Shipping.Infrastructure.Persistence;
using Romp.Modules.Shipping.Infrastructure.Tracking;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Shipping.Tests.Support;

/// <summary>One real PostgreSQL (Testcontainers, CLAUDE.md) with the SHPG migrations applied, shared by the tests of a collection.</summary>
public sealed class ShippingPostgres : IAsyncLifetime
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

    internal ShpgDbContext NewContext()
    {
        var builder = new DbContextOptionsBuilder<ShpgDbContext>().UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "SHPG"));
        return new ShpgDbContext(builder.Options);
    }
}

[CollectionDefinition(Name)]
public sealed class ShippingPostgresFixtureDefinition : ICollectionFixture<ShippingPostgres>
{
    public const string Name = "Shipping PostgreSQL";
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>What Orders would answer: a test sets exactly which orders exist and where they go.</summary>
internal sealed class FakeOrdersShipping : IOrderShippingQueries
{
    public Dictionary<long, OrderShippingView> Views { get; } = [];

    public Task<OrderShippingView?> FindAsync(long orderId, CancellationToken cancellationToken) => Task.FromResult(Views.TryGetValue(orderId, out var v) ? v : null);
}

internal sealed class FakeCaptcha : ICaptchaVerifier
{
    public bool Passes { get; set; }

    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken) => Task.FromResult(Passes && token == "ok");
}

/// <summary>
/// The same MediatR + FluentValidation pipeline <c>ShippingModule</c> wires in production, against the real PostgreSQL, with the pretend couriers
/// (a primary that pushes status and a secondary that must be polled) and a fake Orders.
/// </summary>
internal sealed class ShippingHost : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    public const string PrimarySecret = "test-primary-secret-0123456789abcdef";
    public const string SecondarySecret = "test-secondary-secret-0123456789abcd";

    private static long _orderSeed = 2_000_000;

    private readonly ServiceProvider _root;

    public ShippingHost(ShippingPostgres postgres, Action<ShippingOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedPersistence();
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();

        Time = new FixedTimeProvider(Start);
        services.AddSingleton<TimeProvider>(Time);

        Options = new ShippingOptions();
        Options.Couriers["PRIMARY"] = new CourierSettings { WebhookSecret = PrimarySecret, PushesStatus = true };
        Options.Couriers["SECONDARY"] = new CourierSettings { WebhookSecret = SecondarySecret, PushesStatus = false };
        configure?.Invoke(Options);
        services.AddSingleton<IOptions<ShippingOptions>>(Microsoft.Extensions.Options.Options.Create(Options));

        CourierState = new FakeCourierState();
        Orders = new FakeOrdersShipping();
        Captcha = new FakeCaptcha();
        services.AddSingleton(CourierState);
        services.AddSingleton<ICourierRegistry>(sp => new CourierRegistry(sp.GetRequiredService<IOptions<ShippingOptions>>(), CourierState));
        services.AddSingleton<IOrderShippingQueries>(Orders);
        services.AddSingleton<ICaptchaVerifier>(Captcha);
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton<ITrackingThrottle, TrackingThrottle>();

        services.AddDbContext<ShpgDbContext>((sp, o) =>
        {
            o.UseNpgsql(postgres.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "SHPG"));
            o.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>(), sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });
        services.AddScoped<IShippingDbContext>(sp => sp.GetRequiredService<ShpgDbContext>());
        services.AddScoped<ShipmentBooker>();
        services.AddScoped<IShipmentTrackingQueries, ShipmentTrackingQueries>();
        services.AddSingleton(NullLoggerFactory.Instance);

        var assembly = typeof(AssemblyReference).Assembly;
        services.AddValidatorsFromAssembly(assembly);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(ShippingTransactionBehavior<,>));
        });

        _root = services.BuildServiceProvider();
    }

    public FixedTimeProvider Time { get; }

    public ShippingOptions Options { get; }

    public FakeCourierState CourierState { get; }

    public FakeOrdersShipping Orders { get; }

    public FakeCaptcha Captcha { get; }

    public const string Staff = "Ops Person";

    /// <summary>A packed order that exists in Orders: cash on delivery or paid, going to Lahore.</summary>
    public (long OrderId, string OrderNumber) PlaceOrder(bool cod = true, string address = "House 4, Street 9")
    {
        var id = Interlocked.Increment(ref _orderSeed);
        var number = $"RMP-S{id}";
        Orders.Views[id] = new OrderShippingView(id, number, "CNFD", "Sara Khan", "03001234567", address, "Near the park", "Lahore", "Punjab", "54000", "Call first", cod, cod ? 4700m : 0m);
        return (id, number);
    }

    /// <summary>Creates the shipment the way the order-packed consumer does (without booking) and returns its id.</summary>
    public async Task<long> NewShipmentAsync(bool cod = true, string address = "House 4, Street 9", int grams = 600)
    {
        var (orderId, number) = PlaceOrder(cod, address);
        var created = await SendAsync(new CreateShipmentCommand(orderId, number, grams));
        return created.ShipmentId!.Value;
    }

    public async Task<long> NewBookedShipmentAsync(string courier = "PRIMARY")
    {
        var id = await NewShipmentAsync();
        if (courier == "SECONDARY")
        {
            CourierState.Outage[Couriers.Primary] = true;
        }

        await SendAsync(new BookShipmentCommand(id));
        CourierState.Outage[Couriers.Primary] = false;
        return id;
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

    /// <summary>A push as the primary courier would send it: signed over the raw body.</summary>
    public (string Body, string Signature) Push(string waybill, string status, string? eventId = null, DateTimeOffset? at = null, string secret = PrimarySecret)
    {
        var body = JsonSerializer.Serialize(new { waybill, status, eventId = eventId ?? Guid.NewGuid().ToString("N"), at = at ?? Time.Now });
        return (body, FakeCourierGateway.Sign(secret, body));
    }

    public static async Task<Shipment> LoadAsync(ShippingPostgres postgres, long id)
    {
        await using var db = postgres.NewContext();
        return await db.Shipments.AsNoTracking().Include(s => s.Events).SingleAsync(s => s.Id == id);
    }

    public void Dispose() => _root.Dispose();
}
