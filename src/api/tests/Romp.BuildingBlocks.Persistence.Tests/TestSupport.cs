using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;

namespace Romp.BuildingBlocks.Persistence.Tests;

/// <summary>A minimal aggregate, used only by these tests, that exercises IAuditable + domain events.</summary>
internal sealed class TestAggregate : AggregateRoot<Guid>, IAuditable
{
    public TestAggregate(string name)
    {
        Id = Guid.NewGuid();
        Name = name;
    }

    private TestAggregate()
    {
        Name = string.Empty;
    }

    public string Name { get; set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public void RaiseTestEvent() => Raise(new TestDomainEvent());
}

internal sealed record TestDomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>Lets dispatcher tests (SCRUM-93 task 10) tell claimed/delivered messages apart without a distinct CLR event type per row.</summary>
    public string Label { get; init; } = "";
}

internal sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestAggregate>(builder =>
        {
            builder.HasKey(a => a.Id);
            builder.HasAuditColumns();
        });

        modelBuilder.HasOutboxTable("TEST");
    }
}

internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <summary>Mutable so dispatcher retry/backoff tests (SCRUM-93 task 9) can advance past a scheduled NXT_ATMP_DTE without a real wait.</summary>
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeCurrentActor(string userName) : ICurrentActor
{
    public string UserName => userName;
}

/// <summary>Records every delivery it receives, for dispatcher tests to assert against (SCRUM-93 task 7).</summary>
internal sealed class RecordingOutboxMessageHandler : IOutboxMessageHandler<TestDomainEvent>
{
    public List<TestDomainEvent> Received { get; } = [];

    public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        Received.Add(domainEvent);
        return Task.CompletedTask;
    }
}

/// <summary>Always throws, for dispatcher retry/backoff/dead-letter tests (SCRUM-93 task 9).</summary>
internal sealed class FailingOutboxMessageHandler : IOutboxMessageHandler<TestDomainEvent>
{
    public int AttemptCount { get; private set; }

    public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        AttemptCount++;
        throw new InvalidOperationException("Simulated handler failure.");
    }
}

/// <summary>Records every delivery attempt (by Label) and throws only for the labels named at construction - for the per-aggregate-ordering test (SCRUM-93 task 10), where one aggregate's blocking message must eventually dead-letter without touching the others.</summary>
internal sealed class SelectiveFailureOutboxMessageHandler(params string[] labelsToFail) : IOutboxMessageHandler<TestDomainEvent>
{
    private readonly HashSet<string> _labelsToFail = [.. labelsToFail];

    public List<string> Received { get; } = [];

    public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        Received.Add(domainEvent.Label);
        if (_labelsToFail.Contains(domainEvent.Label))
        {
            throw new InvalidOperationException($"Simulated failure for {domainEvent.Label}.");
        }

        return Task.CompletedTask;
    }
}
