using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Vendor.Tests.Persistence;

/// <summary>Sprint 3: VNDR's first consumer inbox (INBX) and the additive construction-change flag on revisions - real PostgreSQL (Testcontainers).</summary>
public sealed class Sprint3MigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private VendorDbContext NewContext() =>
        new(new DbContextOptionsBuilder<VendorDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "VNDR")).Options);

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Migrate_Inbox_HasCompositeKey()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();

        context.Set<InboxMessage>().Add(new InboxMessage { MessageId = 1, HandlerName = "PROD/StartProductionOnProductionStarted", ProcessedDte = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync();

        // The same message for another handler is a different row; the same message for the same handler is refused.
        await using var second = NewContext();
        second.Set<InboxMessage>().Add(new InboxMessage { MessageId = 1, HandlerName = "PROD/ApplyDeliveryOnDeliveryNoteRecorded" });
        await second.SaveChangesAsync();

        await using var duplicate = NewContext();
        duplicate.Set<InboxMessage>().Add(new InboxMessage { MessageId = 1, HandlerName = "PROD/StartProductionOnProductionStarted" });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public async Task Migrate_ExistingRevisions_DefaultToCommercial()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """SELECT column_default, is_nullable FROM information_schema.columns WHERE table_schema = 'VNDR' AND table_name = 'PO_REV' AND column_name = 'CNST_CHNG_IND'""",
            connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal("false", reader.GetString(0));
        Assert.Equal("NO", reader.GetString(1));
    }

    [Fact]
    public async Task Model_HasNoPendingChanges()
    {
        await using var context = NewContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }
}
