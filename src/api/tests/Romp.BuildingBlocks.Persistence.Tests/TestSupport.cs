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
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeCurrentActor(string userName) : ICurrentActor
{
    public string UserName => userName;
}
