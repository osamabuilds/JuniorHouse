using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Auditing;

namespace Romp.BuildingBlocks.Persistence.Tests.Auditing;

/// <summary>
/// SCRUM-162: proves AuditSaveChangesInterceptor stamps INSR_DTE/INSR_BY on insert and
/// UPDT_DTE/UPDT_BY on update, and only those - EF's InMemory provider is appropriate here
/// because this tests our own interceptor logic, not any PostgreSQL-specific behaviour
/// (CLAUDE.md reserves Testcontainers for tests that need the real database engine).
/// </summary>
public sealed class AuditInterceptorTests
{
    private static TestDbContext CreateContext(string databaseName, DateTimeOffset now, string actor) =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(new AuditSaveChangesInterceptor(new FakeTimeProvider(now), new FakeCurrentActor(actor)))
            .Options);

    [Fact]
    [Trait("Spec", "SCRUM-162")]
    public async Task SaveChanges_NewEntity_SetsInsrDteAndInsrBy()
    {
        var now = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
        using var context = CreateContext(Guid.NewGuid().ToString(), now, "creator");
        var aggregate = new TestAggregate("widget");

        context.Aggregates.Add(aggregate);
        await context.SaveChangesAsync();

        Assert.Equal(now, aggregate.InsrDte);
        Assert.Equal("creator", aggregate.InsrBy);
        Assert.Null(aggregate.UpdtDte);
        Assert.Null(aggregate.UpdtBy);
    }

    [Fact]
    [Trait("Spec", "SCRUM-162")]
    public async Task SaveChanges_ModifiedEntity_SetsUpdtDteAndUpdtBy()
    {
        var databaseName = Guid.NewGuid().ToString();
        var insertedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);
        var aggregateId = Guid.Empty;

        using (var insertContext = CreateContext(databaseName, insertedAt, "creator"))
        {
            var aggregate = new TestAggregate("widget");
            aggregateId = aggregate.Id;
            insertContext.Aggregates.Add(aggregate);
            await insertContext.SaveChangesAsync();
        }

        var updatedAt = insertedAt.AddHours(1);
        using var updateContext = CreateContext(databaseName, updatedAt, "editor");
        var loaded = await updateContext.Aggregates.SingleAsync(a => a.Id == aggregateId);

        loaded.Name = "widget-v2";
        await updateContext.SaveChangesAsync();

        Assert.Equal(insertedAt, loaded.InsrDte);
        Assert.Equal("creator", loaded.InsrBy);
        Assert.Equal(updatedAt, loaded.UpdtDte);
        Assert.Equal("editor", loaded.UpdtBy);
    }
}
