using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Storefront.Domain.StorefrontSettings;
using Romp.Modules.Storefront.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Storefront.Tests.Persistence;

/// <summary>
/// SCRUM-206 task 4, ST-16: the STFR migration against a real PostgreSQL (Testcontainers, CLAUDE.md) - lookups seeded with stable ids,
/// the tables and constraints as designed, every foreign key indexed, one current snapshot, and the EF model matching the migration.
/// </summary>
public sealed class StfrMigrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private StfrDbContext NewContext() =>
        new(new DbContextOptionsBuilder<StfrDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "STFR"))
            .Options);

    private static T Stamp<T>(T entity)
        where T : Romp.BuildingBlocks.Domain.IAuditable
    {
        entity.InsrDte = Now;
        entity.InsrBy = "test";
        return entity;
    }

    [Fact]
    [Trait("Spec", "ST-16")]
    public async Task Migrate_Lookups_SeededWithTheEnumIdsAndStableCodes()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();

        Assert.Equal(["LINK", "GROUP", "COLLECTION_LIST"], await context.Set<NavKindLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(["PATH", "PAGE", "COLLECTION", "CATEGORY", "EXTERNAL"], await context.Set<LinkTargetLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(["DRAFT", "PUBLISHED", "ARCHIVED"], await context.Set<PageStatusLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(["HEADING", "PARAGRAPH", "LIST", "TABLE", "CALLOUT", "FACT"], await context.Set<BlockTypeLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(
            ["BRAND_NAME", "TAGLINE", "WHATSAPP", "EMAIL", "HOURS", "ANNOUNCEMENT", "ANNOUNCEMENT_ON"],
            await context.Set<SettingKeyLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(SettingsFacts.Keys, await context.Set<FactLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
    }

    [Fact]
    [Trait("Spec", "ST-16")]
    public async Task Migrate_Model_HasNoPendingChanges()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    [Trait("Spec", "ST-16")]
    public async Task Migrate_Schema_HasTheTablesUniquesAndIndexedForeignKeys()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        var tables = await ScalarListAsync(connection, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'STFR' ORDER BY table_name");
        foreach (var expected in new[]
        {
            "NAV_ITEM", "AGE_STEP", "FOOT_COLM", "FOOT_LINK", "STNG_ITEM", "POPL_SRCH", "DLVR_RULE", "XCHG_POLY", "XCHG_COND",
            "PAGE_MAIN", "PAGE_BLCK", "PAGE_BLCK_ITM", "PAGE_RDIR", "DRFT_MAIN", "PUBL_SNAP",
            "NAV_KIND_LKP", "LINK_TGT_LKP", "STNG_KEY_LKP", "PAGE_STS_LKP", "BLCK_TYP_LKP", "FACT_LKP",
        })
        {
            Assert.Contains(expected, tables);
        }

        var uniques = await ScalarListAsync(connection, "SELECT indexdef FROM pg_indexes WHERE schemaname = 'STFR' AND indexdef LIKE 'CREATE UNIQUE%'");
        Assert.Contains(uniques, u => u.Contains("\"NAV_ITEM\"") && u.Contains("(\"ITEM_KEY\")"));
        Assert.Contains(uniques, u => u.Contains("\"AGE_STEP\"") && u.Contains("(\"AGE_CODE\")"));
        Assert.Contains(uniques, u => u.Contains("\"PAGE_MAIN\"") && u.Contains("(\"SLUG\")"));
        Assert.Contains(uniques, u => u.Contains("\"PAGE_RDIR\"") && u.Contains("(\"OLD_SLUG\")"));
        Assert.Contains(uniques, u => u.Contains("\"PUBL_SNAP\"") && u.Contains("(\"VER_NO\")"));

        // Every declared foreign key column is covered by an index (docs/db/naming.md rule 3).
        var unindexed = await ScalarListAsync(connection, """
            SELECT c.conrelid::regclass::text || '.' || a.attname
            FROM pg_constraint c
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
            WHERE c.contype = 'f' AND c.connamespace = '"STFR"'::regnamespace
              AND NOT EXISTS (
                SELECT 1 FROM pg_index i
                WHERE i.indrelid = c.conrelid AND i.indkey[0] = c.conkey[1])
            """);
        Assert.Empty(unindexed);
    }

    [Fact]
    [Trait("Spec", "ST-3")]
    [Trait("Spec", "ST-16")]
    public async Task Snapshots_TwoCurrentRows_AreRefusedByTheDatabase()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();
        context.Snapshots.Add(Stamp(PublishedSnapshot.Create(1, "{\"a\":1}", "first")));
        await context.SaveChangesAsync();

        context.Snapshots.Add(Stamp(PublishedSnapshot.Create(2, "{\"a\":2}", "second")));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("UQ_PUBL_SNAP_CRNT_IND", ex.InnerException?.Message);
    }

    [Fact]
    [Trait("Spec", "ST-3")]
    public async Task Snapshots_RetireThenPublish_HasExactlyOneCurrent()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();
        var first = Stamp(PublishedSnapshot.Create(1, "{\"a\":1}", "first"));
        context.Snapshots.Add(first);
        await context.SaveChangesAsync();

        first.Retire();
        context.Snapshots.Add(Stamp(PublishedSnapshot.Create(2, "{\"a\":2}", "second")));
        await context.SaveChangesAsync();

        Assert.Equal([2], await context.Snapshots.Where(s => s.IsCurrent).Select(s => s.VersionNo).ToListAsync());
    }

    [Fact]
    [Trait("Spec", "ST-1a")]
    public async Task NavItems_ThreeLevelTree_RoundTripsInOrder()
    {
        await using (var write = NewContext())
        {
            await write.Database.MigrateAsync();
            var leaf = Stamp(new NavItem("t-frocks", "Frocks", NavKind.Link, new LinkTarget(LinkTargetKind.Path, "/girls/dresses/frocks"), 1));
            var mid = Stamp(new NavItem("t-dresses", "Dresses", NavKind.Link, new LinkTarget(LinkTargetKind.Path, "/girls/dresses"), 1)).AddChild(leaf);
            var root = Stamp(new NavItem("t-girls", "Girls", NavKind.Link, new LinkTarget(LinkTargetKind.Path, "/girls"), 9, showAges: true)).AddChild(mid);
            write.NavItems.Add(root);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext();
        var all = await read.NavItems.Include(n => n.Children).ToListAsync();
        var girls = all.Single(n => n.Key == "t-girls");

        Assert.True(girls.ShowAges);
        Assert.Equal(new LinkTarget(LinkTargetKind.Path, "/girls"), girls.Target);
        Assert.Equal("t-dresses", Assert.Single(girls.Children).Key);
        Assert.Equal("t-frocks", Assert.Single(girls.Children[0].Children).Key);
    }

    [Fact]
    [Trait("Spec", "ST-17")]
    public async Task Pages_BlocksAndFacts_RoundTripInOrder()
    {
        await using (var write = NewContext())
        {
            await write.Database.MigrateAsync();
            var page = Stamp(new Page("t-delivery", "Delivery", "How we deliver."));
            page.ReplaceBlocks(
            [
                Stamp(new PageBlock(BlockType.Heading, "Delivery", level: 2)),
                Stamp(new PageBlock(BlockType.Fact, factKey: SettingsFacts.DeliveryRules, label: "Rates")),
                Stamp(new PageBlock(BlockType.List, items: [Stamp(new PageBlockItem(1, 0, "Lahore")), Stamp(new PageBlockItem(2, 0, "Karachi"))])),
            ]);
            write.Pages.Add(page);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext();
        var loaded = await read.Pages.Include(p => p.Blocks).ThenInclude(b => b.Items).SingleAsync(p => p.Slug == "t-delivery");
        var blocks = loaded.Blocks.OrderBy(b => b.SortSeq).ToList();

        Assert.Equal([BlockType.Heading, BlockType.Fact, BlockType.List], blocks.Select(b => b.Type).ToArray());
        Assert.Equal(SettingsFacts.DeliveryRules, blocks[1].FactKey);
        Assert.Equal(["Lahore", "Karachi"], blocks[2].Items.OrderBy(i => i.RowNo).Select(i => i.Text).ToArray());
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public async Task DeliveryRules_NegativeChargeOrBackwardsDays_AreRefusedByTheDatabase()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();

        context.DeliveryRules.Add(Stamp(new DeliveryRule("Lahore", 2, 3, -5m, null, "TCS")));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("CK_DLVR_RULE_AMT_POS", ex.InnerException?.Message);
    }

    private static async Task<List<string>> ScalarListAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
