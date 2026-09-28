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

    private (OutboxDispatcher Dispatcher, ServiceProvider Provider) BuildDispatcher(RecordingOutboxMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOutboxMessageHandler<TestDomainEvent>>(handler);
        var provider = services.BuildServiceProvider();

        var registration = new OutboxModuleRegistration("TEST", [typeof(TestDomainEvent)]);
        var dispatcherOptions = new OutboxDispatcherOptions { ConnectionString = _postgres.GetConnectionString() };
        var dispatcher = new OutboxDispatcher(
            [registration],
            provider.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            dispatcherOptions,
            NullLogger<OutboxDispatcher>.Instance);

        return (dispatcher, provider);
    }
}
