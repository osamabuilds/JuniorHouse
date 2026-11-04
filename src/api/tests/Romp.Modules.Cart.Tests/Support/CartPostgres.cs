using Microsoft.EntityFrameworkCore;
using Romp.Modules.Cart.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Cart.Tests.Support;

/// <summary>One real PostgreSQL per test class, migrated with the module's own migrations (CLAUDE.md: Testcontainers, never an in-memory database).</summary>
public sealed class CartPostgres : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var context = NewContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync().AsTask();

    internal CartDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "CART"))
            .Options;
        return new CartDbContext(options);
    }
}
