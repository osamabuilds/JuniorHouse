using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Romp.Modules.Quality.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Quality.Tests.Support;

/// <summary>Counts the SQL reader commands a context runs, to prove a list is a constant number of queries (no N+1, CONVENTIONS section 4).</summary>
internal sealed class CommandCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => _count;

    public void Reset() => _count = 0;

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

/// <summary>A real PostgreSQL (Testcontainers, CLAUDE.md) with the QC migrations applied, for tests whose behaviour is SQL.</summary>
internal sealed class QcPostgres : IAsyncDisposable
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public static async Task<QcPostgres> StartAsync()
    {
        var instance = new QcPostgres();
        await instance._postgres.StartAsync();
        await using var context = instance.NewContext();
        await context.Database.MigrateAsync();
        return instance;
    }

    public QcDbContext NewContext(CommandCounter? counter = null)
    {
        var builder = new DbContextOptionsBuilder<QcDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "QC"));
        if (counter is not null)
        {
            builder.AddInterceptors(counter);
        }

        return new QcDbContext(builder.Options);
    }

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();
}
