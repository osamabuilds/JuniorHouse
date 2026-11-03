using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Infrastructure.Persistence;
using Romp.Modules.Orders.Contracts;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Notifications.Tests.Delivery;

/// <summary>A stand-in for an Orders aggregate: it raises the real <see cref="OrderPlacedEvent"/> into the real outbox.</summary>
internal sealed class PlacedOrderStub : AggregateRoot<long>
{
    private PlacedOrderStub()
    {
    }

    public static PlacedOrderStub Place(long id)
    {
        var order = new PlacedOrderStub { Id = id };
        order.Raise(new OrderPlacedEvent(id, "RMP-0001", "key-" + id, "COD", null));
        return order;
    }
}

internal sealed class OrdersStandInContext(DbContextOptions<OrdersStandInContext> options) : DbContext(options)
{
    public DbSet<PlacedOrderStub> Orders => Set<PlacedOrderStub>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ORDR");
        modelBuilder.HasOutboxTable("ORDR");
        modelBuilder.Entity<PlacedOrderStub>(b =>
        {
            b.ToTable("ORDR_STUB");
            b.HasKey(o => o.Id);
            b.Property(o => o.Id).HasColumnName("ID").ValueGeneratedNever();
            b.Ignore(o => o.DomainEvents);
        });
    }
}

/// <summary>An order source the test can switch off, to stand for "Notifications cannot do its work right now".</summary>
internal sealed class SwitchableOrders : IOrderNotificationQueries
{
    public bool Down { get; set; } = true;

    public Task<OrderNotificationView?> FindAsync(long orderId, CancellationToken cancellationToken) =>
        Down
            ? throw new InvalidOperationException("Notifications subsystem is down.")
            : Task.FromResult<OrderNotificationView?>(new OrderNotificationView(orderId, "RMP-0001", "COD", "A", "a@example.com", "+923001234567", false, 1m, []));
}

/// <summary>
/// DLV-5, TC-NOTIF-07, NFR-FT-08: the business transaction never fails because of Notifications. The order and its event commit together;
/// the consumer fails afterwards, in the dispatcher, so the order is untouched; the event waits and is handled once Notifications is back.
/// </summary>
public sealed class NotificationOutageTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var ntfy = NewNtfy();
        await ntfy.Database.MigrateAsync();
        await ntfy.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS \"ORDR\"");
        await using var orders = NewOrders();
        await orders.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private NtfyDbContext NewNtfy() => new(new DbContextOptionsBuilder<NtfyDbContext>()
        .UseNpgsql(_postgres.GetConnectionString(), n => n.MigrationsHistoryTable("MIG_HIST", "NTFY")).Options);

    private OrdersStandInContext NewOrders() => new(new DbContextOptionsBuilder<OrdersStandInContext>()
        .UseNpgsql(_postgres.GetConnectionString())
        .AddInterceptors(new OutboxSaveChangesInterceptor(TimeProvider.System)).Options);

    private OutboxDispatcher Dispatcher(SwitchableOrders orders)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<OutboxMessageContext>();
        services.AddScoped<InboxGuard>();
        services.AddSingleton<IOrderNotificationQueries>(orders);
        services.AddDbContext<NtfyDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString(), n => n.MigrationsHistoryTable("MIG_HIST", "NTFY")));
        services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NtfyDbContext>());
        services.AddSingleton(new Romp.Modules.Notifications.Application.Channels.WhatsAppSettings());
        services.AddScoped<Romp.Modules.Notifications.Application.Channels.ISuppressionList, Romp.Modules.Notifications.Application.Channels.SuppressionList>();
        services.AddScoped<CreateNotification>();
        services.AddScoped<IOutboxMessageHandler<OrderPlacedEvent>, OrderPlacedNotificationConsumer>();
        var provider = services.BuildServiceProvider();
        return new OutboxDispatcher(
            [new OutboxModuleRegistration("ORDR", [typeof(OrderPlacedEvent)])],
            provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            new OutboxDispatcherOptions { ConnectionString = _postgres.GetConnectionString() }, NullLogger<OutboxDispatcher>.Instance);
    }

    [Fact]
    [Trait("Spec", "DLV-5")]
    [Trait("Spec", "TC-NOTIF-07")]
    [Trait("Spec", "NFR-FT-08")]
    public async Task OrderPlaced_WhenSubsystemDown_TheOrderStandsAndTheEventIsHandledAfterRecovery()
    {
        // The order commits with its event, whatever state Notifications is in.
        await using (var orders = NewOrders())
        {
            orders.Orders.Add(PlacedOrderStub.Place(9001));
            await orders.SaveChangesAsync();
        }

        var switchable = new SwitchableOrders { Down = true };
        var dispatcher = Dispatcher(switchable);

        // Notifications is down: the dispatch fails, nothing is created, the order is untouched, the event is kept for a retry.
        var delivered = await dispatcher.DispatchOnceAsync(CancellationToken.None);
        Assert.Equal(0, delivered);
        await using (var orders = NewOrders())
        {
            Assert.True(await orders.Orders.AnyAsync(o => o.Id == 9001));
            var row = await orders.Set<OutboxMessage>().SingleAsync();
            Assert.Null(row.ProcessedDte);
            Assert.False(row.IsDeadLettered);
            Assert.Equal(1, row.AttemptCount);
        }

        await using (var ntfy = NewNtfy())
        {
            Assert.Equal(0, await ntfy.Notifications.CountAsync());
            await ntfy.Database.ExecuteSqlRawAsync("UPDATE \"ORDR\".\"OUTB_MSG\" SET \"NXT_ATMP_DTE\" = NULL");
        }

        // Notifications is back: the same event is handled, once.
        switchable.Down = false;
        Assert.Equal(1, await dispatcher.DispatchOnceAsync(CancellationToken.None));
        Assert.Equal(0, await dispatcher.DispatchOnceAsync(CancellationToken.None));

        await using var after = NewNtfy();
        Assert.Equal(1, await after.Notifications.CountAsync(n => n.ReferenceId == 9001));
    }
}
