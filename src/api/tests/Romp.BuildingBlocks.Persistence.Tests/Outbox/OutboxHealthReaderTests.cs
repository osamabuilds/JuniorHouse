using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Outbox;
using Testcontainers.PostgreSql;

namespace Romp.BuildingBlocks.Persistence.Tests.Outbox;

/// <summary>SCRUM-186: the outbox health read model counts each state correctly, per schema, on a real PostgreSQL.</summary>
public sealed class OutboxHealthReaderTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    [Trait("Spec", "SCRUM-186")]
    public async Task Counts_ByState()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using (var context = new TestDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            var now = DateTimeOffset.UtcNow;
            context.OutboxMessages.AddRange(
                Row(now.AddMinutes(-10)),                                                                       // pending, oldest
                Row(now.AddMinutes(-2)),                                                                        // pending
                Row(now.AddMinutes(-1), claimedBy: "x", leaseExpiry: now.AddMinutes(1)),                        // in flight
                Row(now.AddMinutes(-1), attempts: 2, nextAttempt: now.AddMinutes(5)),                           // retrying
                Row(now.AddMinutes(-30), attempts: 5, deadLettered: true),                                      // gave up
                Row(now.AddMinutes(-40), processed: now.AddMinutes(-39)));                                      // done: counted nowhere
            await context.SaveChangesAsync();
        }

        var reader = new OutboxHealthReader(
            [new OutboxModuleRegistration("TEST", [typeof(TestDomainEvent)])],
            new OutboxDispatcherOptions { ConnectionString = _postgres.GetConnectionString() });

        var health = Assert.Single(await reader.ReadAsync(CancellationToken.None));

        Assert.Equal("TEST", health.Schema);
        Assert.Equal(2, health.Pending);
        Assert.Equal(1, health.InFlight);
        Assert.Equal(1, health.Retrying);
        Assert.Equal(1, health.DeadLettered);
        Assert.InRange(health.OldestPendingSeconds!.Value, 9 * 60, 11 * 60); // the 10-minute-old pending row
    }

    [Fact]
    [Trait("Spec", "SCRUM-186")]
    public async Task EmptyOutbox_ReportsZeroesAndNoOldestAge()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using (var context = new TestDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        var reader = new OutboxHealthReader(
            [new OutboxModuleRegistration("TEST", [typeof(TestDomainEvent)])],
            new OutboxDispatcherOptions { ConnectionString = _postgres.GetConnectionString() });

        var health = Assert.Single(await reader.ReadAsync(CancellationToken.None));

        Assert.Equal((0, 0, 0, 0), (health.Pending, health.InFlight, health.Retrying, health.DeadLettered));
        Assert.Null(health.OldestPendingSeconds);
    }

    private static OutboxMessage Row(
        DateTimeOffset insertedAt, string? claimedBy = null, DateTimeOffset? leaseExpiry = null, short attempts = 0,
        DateTimeOffset? nextAttempt = null, bool deadLettered = false, DateTimeOffset? processed = null) => new()
    {
        EventType = nameof(TestDomainEvent),
        Payload = "{}",
        InsrDte = insertedAt,
        ClaimedBy = claimedBy,
        LeaseExpiryDte = leaseExpiry,
        AttemptCount = attempts,
        NextAttemptDte = nextAttempt,
        IsDeadLettered = deadLettered,
        ProcessedDte = processed,
    };
}
