using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Romp.BuildingBlocks.Persistence;
using Romp.Modules.Vendor.Infrastructure;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Vendor.Tests;

/// <summary>
/// SCRUM-93 task 6: the additive `OUTB_MSG` migration must not break Sprint 1's existing,
/// already-written rows (AC-52). Uses Testcontainers (CLAUDE.md); Windows Application Control
/// blocks Docker-backed tests on this machine (CLAUDE.md) - written and build-verified here, run
/// on CI.
/// </summary>
public sealed class OutboxMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    [Trait("Spec", "AC-52")]
    public async Task Migrate_AddsDispatcherColumns_ExistingRowsGetSafeDefaults()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

        // Migrate only as far as Sprint 1 left off, then write two rows the way Sprint 1's
        // OutboxSaveChangesInterceptor did - none of the dispatcher columns exist yet at this
        // point. One payload has the "PoId" shape every Sprint 1 event serializes (AC-52's
        // backfill); the other simulates an unrecognisable payload that must not fail the migration.
        await using (var preContext = new VendorDbContext(options))
        {
            await preContext.Database.GetService<IMigrator>()
                .MigrateAsync("20260927214015_InitialVendorsAndPurchaseOrders");
            // ExecuteSqlRawAsync treats the string as a composite format - literal braces in the
            // JSON payload must be doubled or it throws trying to parse them as {n} placeholders.
            await preContext.Database.ExecuteSqlRawAsync(
                """INSERT INTO "VNDR"."OUTB_MSG" ("EVNT_TYP", "PYLD", "INSR_DTE") VALUES ('PoCreatedEvent', '{{"PoId":42,"PoNo":"PO-2026-00001","VendorId":1,"StyleId":1}}', now())""");
            await preContext.Database.ExecuteSqlRawAsync(
                """INSERT INTO "VNDR"."OUTB_MSG" ("EVNT_TYP", "PYLD", "INSR_DTE") VALUES ('SomeOtherEvent', '{{}}', now())""");
        }

        await using var context = new VendorDbContext(options);
        await context.Database.MigrateAsync();

        var rows = await context.Set<OutboxMessage>().OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(2, rows.Count);

        var withPoId = rows[0];
        Assert.Equal("PurchaseOrder", withPoId.AggregateType);
        Assert.Equal(42, withPoId.AggregateId);
        Assert.Equal((short)1, withPoId.MessageVersion);
        Assert.Equal((short)0, withPoId.AttemptCount);
        Assert.Null(withPoId.NextAttemptDte);
        Assert.Null(withPoId.ClaimedBy);
        Assert.Null(withPoId.LeaseExpiryDte);
        Assert.Null(withPoId.ProcessedDte);
        Assert.False(withPoId.IsDeadLettered);
        Assert.Null(withPoId.DeadLetterReason);

        var withoutPoId = rows[1];
        Assert.Null(withoutPoId.AggregateId);
    }
}
