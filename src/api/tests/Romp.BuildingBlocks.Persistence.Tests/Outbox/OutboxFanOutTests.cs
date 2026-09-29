using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using System.Text.Json;
using Testcontainers.PostgreSql;

namespace Romp.BuildingBlocks.Persistence.Tests.Outbox;

/// <summary>
/// ADR 0008 (SCRUM-185): the same event can have more than one consumer (the producer's placeholder logger plus a real
/// consumer in another module), every handler runs, and the dispatcher hands each one the outbox row id that the inbox keys on.
/// </summary>
public sealed class OutboxFanOutTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private async Task<DbContextOptions<TestDbContext>> SeedOneRowAsync()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var context = new TestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.OutboxMessages.Add(new OutboxMessage { EventType = nameof(TestDomainEvent), Payload = JsonSerializer.Serialize(new TestDomainEvent()), InsrDte = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync();
        return options;
    }

    private OutboxDispatcher Build(ServiceCollection services)
    {
        services.AddScoped<OutboxMessageContext>();
        var provider = services.BuildServiceProvider();
        return new OutboxDispatcher(
            [new OutboxModuleRegistration("TEST", [typeof(TestDomainEvent)])],
            provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            new OutboxDispatcherOptions { ConnectionString = _postgres.GetConnectionString() }, NullLogger<OutboxDispatcher>.Instance);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Dispatch_TwoHandlersForOneEvent_BothRun_AndBothSeeTheOutboxRowId()
    {
        var options = await SeedOneRowAsync();
        var first = new RecordingWithContext();
        var second = new RecordingWithContext();
        var services = new ServiceCollection();
        services.AddScoped<IOutboxMessageHandler<TestDomainEvent>>(sp => first.Bind(sp));
        services.AddScoped<IOutboxMessageHandler<TestDomainEvent>>(sp => second.Bind(sp));

        var delivered = await Build(services).DispatchOnceAsync(CancellationToken.None);

        Assert.Equal(1, delivered);
        await using var context = new TestDbContext(options);
        var rowId = (await context.OutboxMessages.SingleAsync()).Id;
        Assert.Equal([rowId], first.MessageIds);
        Assert.Equal([rowId], second.MessageIds);
        Assert.Equal("TEST", first.Schemas.Single());
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Dispatch_OneHandlerFails_RowIsRetried_NotMarkedProcessed()
    {
        var options = await SeedOneRowAsync();
        var services = new ServiceCollection();
        services.AddScoped<IOutboxMessageHandler<TestDomainEvent>>(_ => new RecordingWithContext());
        services.AddScoped<IOutboxMessageHandler<TestDomainEvent>>(_ => new FailingOutboxMessageHandler());

        var delivered = await Build(services).DispatchOnceAsync(CancellationToken.None);

        Assert.Equal(0, delivered);
        await using var context = new TestDbContext(options);
        var row = await context.OutboxMessages.SingleAsync();
        Assert.Null(row.ProcessedDte);
        Assert.Equal(1, row.AttemptCount);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task InboxGuard_SecondCallForTheSameMessageAndHandler_IsRefused_ADifferentHandlerIsNot()
    {
        var options = new DbContextOptionsBuilder<InboxTestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using (var setup = new InboxTestDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        var context = new OutboxMessageContext { MessageId = 9, Schema = "VNDR" };
        var guard = new InboxGuard(context, TimeProvider.System);

        await using (var db = new InboxTestDbContext(options))
        {
            Assert.True(await guard.TryBeginAsync(db, "HandlerA", CancellationToken.None));
            await db.SaveChangesAsync();
        }

        await using (var db = new InboxTestDbContext(options))
        {
            Assert.False(await guard.TryBeginAsync(db, "HandlerA", CancellationToken.None)); // redelivery
            Assert.True(await guard.TryBeginAsync(db, "HandlerB", CancellationToken.None));   // another consumer of the same message
            context.MessageId = 10;
            Assert.True(await guard.TryBeginAsync(db, "HandlerA", CancellationToken.None));   // a different message
        }
    }

    private sealed class InboxTestDbContext(DbContextOptions<InboxTestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasInboxTable("TEST");
    }

    private sealed class RecordingWithContext : IOutboxMessageHandler<TestDomainEvent>
    {
        private OutboxMessageContext? _context;

        public List<long> MessageIds { get; } = [];

        public List<string> Schemas { get; } = [];

        public RecordingWithContext Bind(IServiceProvider provider)
        {
            _context = provider.GetRequiredService<OutboxMessageContext>();
            return this;
        }

        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            if (_context is not null)
            {
                MessageIds.Add(_context.MessageId);
                Schemas.Add(_context.Schema);
            }

            return Task.CompletedTask;
        }
    }
}
