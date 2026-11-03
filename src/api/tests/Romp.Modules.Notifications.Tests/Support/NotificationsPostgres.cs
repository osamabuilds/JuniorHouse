using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Notifications.Tests.Support;

/// <summary>One real PostgreSQL per test class, migrated with the module's own migrations (CLAUDE.md: Testcontainers, never an in-memory database).</summary>
public sealed class NotificationsPostgres : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>A context that also writes raised events to the module's outbox, as the running host does.</summary>
    internal NtfyDbContext NewContextWithOutbox()
    {
        var options = new DbContextOptionsBuilder<NtfyDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "NTFY"))
            .AddInterceptors(new Romp.BuildingBlocks.Persistence.Outbox.OutboxSaveChangesInterceptor(TimeProvider.System))
            .Options;
        return new NtfyDbContext(options);
    }

    /// <summary>Empties the notifications so a test that claims "everything due" sees only its own rows.</summary>
    public async Task ResetAsync()
    {
        await using var context = NewContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE \"NTFY\".\"NTFY_MAIN\", \"NTFY\".\"OUTB_MSG\" CASCADE");
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync().AsTask();

    internal NtfyDbContext NewContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<NtfyDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "NTFY"))
            .AddInterceptors(interceptors)
            .Options;
        return new NtfyDbContext(options);
    }
}

/// <summary>Counts the SQL commands a context runs, so a test can prove a page costs a fixed number of queries however many rows it has.</summary>
internal sealed class CommandCounter : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
{
    private int _count;

    public int Count => _count;

    public override System.Data.Common.DbDataReader ReaderExecuted(System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData, System.Data.Common.DbDataReader result)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData, System.Data.Common.DbDataReader result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }
}
