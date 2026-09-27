using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Infrastructure;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Vendor.Tests;

/// <summary>
/// ADR 0006, AC-7b: 20 concurrent allocations against real PostgreSQL never produce a duplicate
/// PO number. Deliberately not mocked (CLAUDE.md) - this specifically proves the
/// INSERT...ON CONFLICT...RETURNING upsert's row-level locking is safe under real concurrency,
/// which an in-memory/fake allocator can't demonstrate. Windows Application Control blocks
/// Docker-backed tests on this machine (CLAUDE.md) - written and build-verified here, run on CI.
/// </summary>
public sealed class PoNumberAllocatorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Allocate_20ConcurrentCalls_ReturnsNoDuplicates()
    {
        // Migrate once, then let each concurrent task use its own DbContext/connection - allocation
        // safety must hold across separate connections, not just within one.
        var migrationOptions = new DbContextOptionsBuilder<VendorDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using (var migrationContext = new VendorDbContext(migrationOptions))
        {
            await migrationContext.Database.MigrateAsync();
        }

        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            var options = new DbContextOptionsBuilder<VendorDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
            await using var context = new VendorDbContext(options);
            var allocator = new PoNumberAllocator(context, TimeProvider.System);
            return await allocator.AllocateAsync(CancellationToken.None);
        });

        var poNumbers = await Task.WhenAll(tasks);

        Assert.Equal(20, poNumbers.Distinct().Count());
    }
}
