using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Romp.BuildingBlocks.Persistence.Tests;

/// <summary>
/// SCRUM-93 task 7 (SCRUM-181): the dispatcher's happy path - claim a committed row, invoke the
/// registered handler outside the claim transaction, mark it Processed only after the handler
/// succeeds (AC-53, AC-54). Uses Testcontainers (CLAUDE.md); Windows Application Control blocks
/// Docker-backed tests on this machine (CLAUDE.md) - written and build-verified here, run on CI.
/// </summary>
public sealed class OutboxDispatcherTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    [Trait("Spec", "AC-53")]
    [Trait("Spec", "AC-54")]
    public async Task Dispatch_CommittedRow_DeliveredAndMarkedProcessed()
    {
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

        await using (var seedContext = new TestDbContext(dbOptions))
        {
            await seedContext.Database.EnsureCreatedAsync();

            var domainEvent = new TestDomainEvent();
            seedContext.OutboxMessages.Add(new OutboxMessage
            {
                EventType = nameof(TestDomainEvent),
                Payload = JsonSerializer.Serialize(domainEvent),
                InsrDte = DateTimeOffset.UtcNow,
            });
            await seedContext.SaveChangesAsync();
        }

        var handler = new RecordingOutboxMessageHandler();
        var (dispatcher, provider) = BuildDispatcher(handler);
        await using var _ = provider;

        var delivered = await dispatcher.DispatchOnceAsync(CancellationToken.None);

        Assert.Equal(1, delivered);
        Assert.Single(handler.Received);

        await using var verifyContext = new TestDbContext(dbOptions);
        var row = await verifyContext.OutboxMessages.SingleAsync();
        Assert.NotNull(row.ProcessedDte);
        Assert.NotNull(row.ClaimedBy);
    }

    [Fact]
    [Trait("Spec", "AC-55")]
    public async Task Dispatch_ExpiredLease_RowBecomesClaimableAgain()
    {
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

        // Simulates a prior dispatcher instance that claimed the row and then crashed/hung before
        // marking it Processed - its lease has since expired (AC-55).
        await using (var seedContext = new TestDbContext(dbOptions))
        {
            await seedContext.Database.EnsureCreatedAsync();

            seedContext.OutboxMessages.Add(new OutboxMessage
            {
                EventType = nameof(TestDomainEvent),
                Payload = JsonSerializer.Serialize(new TestDomainEvent()),
                InsrDte = DateTimeOffset.UtcNow,
                ClaimedBy = "stale-instance:1",
                LeaseExpiryDte = DateTimeOffset.UtcNow.AddMinutes(-5),
            });
            await seedContext.SaveChangesAsync();
        }

        var handler = new RecordingOutboxMessageHandler();
        var (dispatcher, provider) = BuildDispatcher(handler);
        await using var _ = provider;

        var delivered = await dispatcher.DispatchOnceAsync(CancellationToken.None);

        Assert.Equal(1, delivered);
        Assert.Single(handler.Received);

        await using var verifyContext = new TestDbContext(dbOptions);
        var row = await verifyContext.OutboxMessages.SingleAsync();
        Assert.NotNull(row.ProcessedDte);
        Assert.Equal($"{Environment.MachineName}:{Environment.ProcessId}", row.ClaimedBy);
    }

    [Fact]
    [Trait("Spec", "AC-57")]
    [Trait("Spec", "AC-58")]
    public async Task Dispatch_HandlerFailsRepeatedly_BacksOffThenDeadLetters()
    {
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

        await using (var seedContext = new TestDbContext(dbOptions))
        {
            await seedContext.Database.EnsureCreatedAsync();

            seedContext.OutboxMessages.Add(new OutboxMessage
            {
                EventType = nameof(TestDomainEvent),
                Payload = JsonSerializer.Serialize(new TestDomainEvent()),
                InsrDte = DateTimeOffset.UtcNow,
            });
            await seedContext.SaveChangesAsync();
        }

        var handler = new FailingOutboxMessageHandler();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (dispatcher, provider) = BuildDispatcher(
            handler,
            clock,
            o =>
            {
                o.MaxAttempts = 3;
                o.RetryBaseDelay = TimeSpan.FromSeconds(1);
                o.RetryMaxDelay = TimeSpan.FromSeconds(10);
            });
        await using var _ = provider;

        // Attempt 1 fails and is scheduled for retry (not yet dead-lettered - AC-57).
        await dispatcher.DispatchOnceAsync(CancellationToken.None);
        // Attempt 2: advance the clock past the backoff window so the row is claimable again.
        clock.Now = clock.Now.AddMinutes(10);
        await dispatcher.DispatchOnceAsync(CancellationToken.None);
        // Attempt 3 crosses MaxAttempts - dead-lettered (AC-58).
        clock.Now = clock.Now.AddMinutes(10);
        await dispatcher.DispatchOnceAsync(CancellationToken.None);

        Assert.Equal(3, handler.AttemptCount);

        await using var verifyContext = new TestDbContext(dbOptions);
        var row = await verifyContext.OutboxMessages.SingleAsync();
        Assert.True(row.IsDeadLettered);
        Assert.Equal(3, row.AttemptCount);
        Assert.NotNull(row.DeadLetterReason);
        Assert.Null(row.ProcessedDte);
    }

    [Fact]
    [Trait("Spec", "AC-59")]
    public async Task Dispatch_SameAggregateMultipleMessages_DeliveredInOrder_DeadLetterDoesNotBlockLater()
    {
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

        // Aggregate 100 has three messages (A1-A3); aggregate 200 (a different aggregate) has one
        // (B1), inserted in this order so ID order matches insertion order.
        await using (var seedContext = new TestDbContext(dbOptions))
        {
            await seedContext.Database.EnsureCreatedAsync();

            foreach (var (label, aggregateId) in new[] { ("A1", 100L), ("A2", 100L), ("A3", 100L), ("B1", 200L) })
            {
                seedContext.OutboxMessages.Add(new OutboxMessage
                {
                    EventType = nameof(TestDomainEvent),
                    Payload = JsonSerializer.Serialize(new TestDomainEvent { Label = label }),
                    InsrDte = DateTimeOffset.UtcNow,
                    AggregateId = aggregateId,
                });
            }

            await seedContext.SaveChangesAsync();
        }

        // A1 always fails; A2/A3/B1 always succeed. MaxAttempts = 2, so A1 dead-letters on its
        // second failed attempt.
        var handler = new SelectiveFailureOutboxMessageHandler("A1");
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (dispatcher, provider) = BuildDispatcher(
            handler,
            clock,
            o =>
            {
                o.MaxAttempts = 2;
                o.RetryBaseDelay = TimeSpan.FromSeconds(1);
                o.RetryMaxDelay = TimeSpan.FromSeconds(10);
            });
        await using var _ = provider;

        // Pass 1: A1 (earliest open row for aggregate 100) and B1 (independent aggregate) are
        // claimable; A2/A3 are blocked behind A1. A1's attempt 1 fails (not yet dead-lettered).
        await dispatcher.DispatchOnceAsync(CancellationToken.None);
        Assert.Contains("A1", handler.Received);
        Assert.Contains("B1", handler.Received);
        Assert.DoesNotContain("A2", handler.Received);
        Assert.DoesNotContain("A3", handler.Received);

        // Pass 2: A1 is still the sole open row for aggregate 100 (still not dead-lettered going
        // into the claim), so A2/A3 stay blocked. This attempt crosses MaxAttempts -> dead-lettered.
        clock.Now = clock.Now.AddMinutes(10);
        await dispatcher.DispatchOnceAsync(CancellationToken.None);
        Assert.Equal(2, handler.Received.Count(l => l == "A1"));
        Assert.DoesNotContain("A2", handler.Received);

        // Pass 3: A1 is now dead-lettered, so it no longer blocks - A2 becomes claimable, A3 still
        // blocked behind A2.
        await dispatcher.DispatchOnceAsync(CancellationToken.None);
        Assert.Contains("A2", handler.Received);
        Assert.DoesNotContain("A3", handler.Received);

        // Pass 4: A2 is Processed, so A3 is finally claimable.
        await dispatcher.DispatchOnceAsync(CancellationToken.None);
        Assert.Contains("A3", handler.Received);

        await using var verifyContext = new TestDbContext(dbOptions);
        var aggregate100Rows = await verifyContext.OutboxMessages
            .Where(m => m.AggregateId == 100)
            .OrderBy(m => m.Id)
            .ToListAsync();
        Assert.True(aggregate100Rows[0].IsDeadLettered); // A1
        Assert.NotNull(aggregate100Rows[1].ProcessedDte); // A2
        Assert.NotNull(aggregate100Rows[2].ProcessedDte); // A3
    }

    [Fact]
    [Trait("Spec", "AC-60")]
    [Trait("Spec", "NFR-FT-08")]
    public async Task HandlerFailure_DoesNotAffectAlreadyCommittedBusinessRow()
    {
        var dbOptions = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

        await using (var createSchemaContext = new TestDbContext(dbOptions))
        {
            await createSchemaContext.Database.EnsureCreatedAsync();
        }

        // The business transaction: the aggregate and its domain event commit together through
        // the real OutboxSaveChangesInterceptor, exactly as production code does (ADR 0004) - the
        // dispatcher plays no part in this transaction and never could, since it only ever reads
        // rows that already committed.
        Guid aggregateId;
        await using (var businessContext = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new OutboxSaveChangesInterceptor(TimeProvider.System))
            .Options))
        {
            var aggregate = new TestAggregate("widget");
            aggregate.RaiseTestEvent();
            aggregateId = aggregate.Id;
            businessContext.Aggregates.Add(aggregate);
            await businessContext.SaveChangesAsync();
        }

        var handler = new FailingOutboxMessageHandler();
        var (dispatcher, provider) = BuildDispatcher(handler, TimeProvider.System, o => o.MaxAttempts = 1);
        await using var _ = provider;

        await dispatcher.DispatchOnceAsync(CancellationToken.None);

        await using var verifyContext = new TestDbContext(dbOptions);
        var aggregateRow = await verifyContext.Aggregates.SingleAsync(a => a.Id == aggregateId);
        Assert.Equal("widget", aggregateRow.Name);

        var outboxRow = await verifyContext.OutboxMessages.SingleAsync();
        Assert.True(outboxRow.IsDeadLettered); // MaxAttempts = 1: the one failed attempt dead-letters it immediately.
        Assert.Null(outboxRow.ProcessedDte);
    }

    private (OutboxDispatcher Dispatcher, ServiceProvider Provider) BuildDispatcher(RecordingOutboxMessageHandler handler) =>
        BuildDispatcher((IOutboxMessageHandler<TestDomainEvent>)handler, TimeProvider.System, configure: null);

    private (OutboxDispatcher Dispatcher, ServiceProvider Provider) BuildDispatcher(SelectiveFailureOutboxMessageHandler handler, TimeProvider clock, Action<OutboxDispatcherOptions> configure) =>
        BuildDispatcher((IOutboxMessageHandler<TestDomainEvent>)handler, clock, configure);

    private (OutboxDispatcher Dispatcher, ServiceProvider Provider) BuildDispatcher(
        IOutboxMessageHandler<TestDomainEvent> handler, TimeProvider clock, Action<OutboxDispatcherOptions>? configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(handler);
        var provider = services.BuildServiceProvider();

        var registration = new OutboxModuleRegistration("TEST", [typeof(TestDomainEvent)]);
        var dispatcherOptions = new OutboxDispatcherOptions { ConnectionString = _postgres.GetConnectionString() };
        configure?.Invoke(dispatcherOptions);

        var dispatcher = new OutboxDispatcher(
            [registration],
            provider.GetRequiredService<IServiceScopeFactory>(),
            clock,
            dispatcherOptions,
            NullLogger<OutboxDispatcher>.Instance);

        return (dispatcher, provider);
    }
}
