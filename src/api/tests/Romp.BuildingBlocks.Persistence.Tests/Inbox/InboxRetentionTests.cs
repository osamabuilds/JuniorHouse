using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Testcontainers.PostgreSql;

namespace Romp.BuildingBlocks.Persistence.Tests.Inbox;

/// <summary>
/// SCRUM-93 task 14 (AC-62): retention purge for a module's INBX table, plus the startup guard that
/// refuses misconfigured retention (not longer than the max retry window). Not wired to a real
/// module this sprint (task 12's convention has no real consumer yet) - tested against a throwaway
/// "TEST" schema exactly like OutboxDispatcherTests does for OUTB_MSG. Windows Application Control
/// blocks Docker-backed tests on this machine (CLAUDE.md) - written and build-verified here, run on
/// CI.
/// </summary>
public sealed class InboxRetentionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private sealed class InboxTestDbContext(DbContextOptions<InboxTestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasInboxTable("TEST");
    }

    [Fact]
    [Trait("Spec", "AC-62")]
    public async Task Purge_RemovesRowsOlderThanRetention()
    {
        var dbOptions = new DbContextOptionsBuilder<InboxTestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        var now = DateTimeOffset.UtcNow;

        await using (var seedContext = new InboxTestDbContext(dbOptions))
        {
            await seedContext.Database.EnsureCreatedAsync();

            seedContext.Add(new InboxMessage { MessageId = 1, HandlerName = "Old", ProcessedDte = now.AddDays(-10) });
            seedContext.Add(new InboxMessage { MessageId = 2, HandlerName = "Recent", ProcessedDte = now.AddHours(-1) });
            seedContext.Add(new InboxMessage { MessageId = 3, HandlerName = "Unprocessed", ProcessedDte = null });
            await seedContext.SaveChangesAsync();
        }

        var options = new InboxRetentionOptions
        {
            ConnectionString = _postgres.GetConnectionString(),
            RetentionPeriod = TimeSpan.FromDays(7),
            MaxRetryWindow = TimeSpan.FromDays(1),
        };
        var purger = new InboxRetentionPurger(options, new FakeTimeProvider(now));

        var purged = await purger.PurgeAsync("TEST", CancellationToken.None);
        Assert.Equal(1, purged);

        await using var verifyContext = new InboxTestDbContext(dbOptions);
        var remainingIds = await verifyContext.Set<InboxMessage>().Select(m => m.MessageId).ToListAsync();
        Assert.Equal([2, 3], remainingIds.OrderBy(id => id).ToArray());
    }

    [Fact]
    [Trait("Spec", "AC-62")]
    public void Startup_RetentionNotLongerThanRetryWindow_Throws()
    {
        var options = new InboxRetentionOptions
        {
            ConnectionString = "Host=localhost;Database=romp;Username=romp;Password=x",
            RetentionPeriod = TimeSpan.FromHours(1),
            MaxRetryWindow = TimeSpan.FromHours(1),
        };

        Assert.Throws<InvalidOperationException>(() => new InboxRetentionPurger(options, TimeProvider.System));
    }
}
