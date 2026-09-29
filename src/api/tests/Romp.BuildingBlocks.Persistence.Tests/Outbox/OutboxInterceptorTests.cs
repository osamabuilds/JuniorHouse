using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Outbox;

namespace Romp.BuildingBlocks.Persistence.Tests.Outbox;

/// <summary>
/// SCRUM-165 / ADR 0004: proves OutboxSaveChangesInterceptor moves an aggregate's pending domain
/// events into OUTB_MSG as part of the same SaveChanges call that persists the aggregate itself.
/// </summary>
public sealed class OutboxInterceptorTests
{
    [Fact]
    [Trait("Spec", "SCRUM-165")]
    public async Task SaveChanges_WithDomainEvent_WritesOutboxRowInSameTransaction()
    {
        var now = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new OutboxSaveChangesInterceptor(new FakeTimeProvider(now)))
            .Options;

        using var context = new TestDbContext(options);
        var aggregate = new TestAggregate("widget");
        aggregate.RaiseTestEvent();
        context.Aggregates.Add(aggregate);

        await context.SaveChangesAsync();

        var outboxMessage = Assert.Single(context.OutboxMessages);
        Assert.Equal(nameof(TestDomainEvent), outboxMessage.EventType);
        Assert.Equal(now, outboxMessage.InsrDte);
        Assert.Contains("EventId", outboxMessage.Payload);
        Assert.Empty(aggregate.DomainEvents);
    }
}
