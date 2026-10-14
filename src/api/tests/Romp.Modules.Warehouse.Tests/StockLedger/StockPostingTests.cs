using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.StockLedger;
using Romp.Modules.Warehouse.Infrastructure.Persistence;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.StockLedger;

[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class StockPostingTests(WarehousePostgres postgres)
{
    private static StockPost Put(long variant, long bin, int qty, short grade = Grades.Accepted) =>
        new(variant, grade, bin, qty, MovementKinds.Putaway, 77);

    private static int LevelEvents(WhseDbContext db, string sku) =>
        db.Set<OutboxMessage>().Where(m => m.EventType == "StockLevelChangedEvent").AsEnumerable()
            .Count(m => m.Payload.Contains(sku, StringComparison.Ordinal));

    [Fact]
    [Trait("Spec", "LED-1")]
    [Trait("Spec", "NFR-SC-05")]
    public async Task APosting_ChangesTheBalance_AppendsAMovementWithBeforeAndAfter_AndWritesTheEventInTheSameTransaction()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        var posted = await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 10)], "Ayesha Khan"));
        await host.SendAsync(new TestPostCommand([new StockPost(variant.RefId, Grades.Accepted, bin.Id, -4, MovementKinds.Adjustment, 5)], "Bilal Ahmed"));

        Assert.Equal((0, 10), (posted[0].BalanceBefore, posted[0].BalanceAfter));
        await using var db = postgres.NewContext();
        Assert.Equal(6, (await db.Balances.AsNoTracking().SingleAsync(b => b.VariantRefId == variant.RefId)).Quantity);
        var movements = await db.Movements.AsNoTracking().Where(m => m.VariantRefId == variant.RefId).OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal((0, 10, 10, "Ayesha Khan"), (movements[0].BalanceBefore, movements[0].BalanceAfter, movements[0].QuantityChange, movements[0].ActorName));
        Assert.Equal((10, 6, -4, "Bilal Ahmed"), (movements[1].BalanceBefore, movements[1].BalanceAfter, movements[1].QuantityChange, movements[1].ActorName));
        Assert.All(movements, m => Assert.NotEqual(default, m.InsrDte));
        Assert.Equal(2, LevelEvents(db, variant.Sku));
    }

    [Fact]
    [Trait("Spec", "LED-3")]
    [Trait("Spec", "TC-STK-05")]
    public async Task ARemovalBeyondTheBalance_IsRefused_AndNothingChanges()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 3)]));

        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, -4)])));

        Assert.Equal(StockMessages.Insufficient, ex.Message);
        await using var db = postgres.NewContext();
        Assert.Equal(3, (await db.Balances.AsNoTracking().SingleAsync(b => b.VariantRefId == variant.RefId)).Quantity);
        Assert.Equal(1, await db.Movements.CountAsync(m => m.VariantRefId == variant.RefId));
    }

    [Fact]
    [Trait("Spec", "LED-3")]
    public async Task RemovingFromABinThatNeverHeldTheVariant_IsRefused_WithoutCreatingARow()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, -1)])));

        await using var db = postgres.NewContext();
        Assert.False(await db.Balances.AnyAsync(b => b.VariantRefId == variant.RefId));
    }

    [Fact]
    [Trait("Spec", "LED-3")]
    [Trait("Spec", "NFR-FT-08")]
    public async Task ARefusalLaterInTheSameTransaction_RollsBackEarlierChangesAndEvents()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();

        var emptyBin = await host.NewBinAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 5), Put(variant.RefId, emptyBin.Id, -1)])));

        await using var db = postgres.NewContext();
        Assert.False(await db.Balances.AnyAsync(b => b.VariantRefId == variant.RefId));
        Assert.False(await db.Movements.AnyAsync(m => m.VariantRefId == variant.RefId));
        Assert.Equal(0, LevelEvents(db, variant.Sku));
    }

    [Fact]
    [Trait("Spec", "LED-4")]
    public async Task ManyChangesToOneVariantInOneTransaction_PublishExactlyOneEvent_WithTheGradeSplit()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var binA = await host.NewBinAsync();
        var binB = await host.NewBinAsync();

        await host.SendAsync(new TestPostCommand([
            Put(variant.RefId, binA.Id, 6), Put(variant.RefId, binB.Id, 4), Put(variant.RefId, binA.Id, 3, Grades.Concession)]));

        await using var db = postgres.NewContext();
        var message = Assert.Single(
            db.Set<OutboxMessage>().Where(m => m.EventType == "StockLevelChangedEvent").AsEnumerable().ToList(),
            m => m.Payload.Contains(variant.Sku, StringComparison.Ordinal));
        Assert.Contains("\"OnHand\": 13", message.Payload, StringComparison.Ordinal);
        Assert.Contains("\"OnHandAccepted\": 10", message.Payload, StringComparison.Ordinal);
        Assert.Contains("\"OnHandConcession\": 3", message.Payload, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "LED-3")]
    [Trait("Spec", "TC-STK-07")]
    public async Task FiftyParallelRemovalsAgainstThirtyUnits_ApplyExactlyThirty_AndTheBalanceNeverGoesNegative()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 30)]));

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(async _ =>
        {
            try
            {
                await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, -1)]));
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }));

        Assert.Equal(30, results.Count(applied => applied));
        await using var db = postgres.NewContext();
        Assert.Equal(0, (await db.Balances.AsNoTracking().SingleAsync(b => b.VariantRefId == variant.RefId)).Quantity);
        Assert.Equal(31, await db.Movements.CountAsync(m => m.VariantRefId == variant.RefId));
    }

    [Fact]
    [Trait("Spec", "LED-2")]
    public async Task TheLedgerSum_AlwaysEqualsTheStoredBalances_AfterRandomMixedOperations()
    {
        using var host = new WarehouseHost(postgres);
        var variants = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            variants.Add((await host.NewVariantAsync()).RefId);
        }

        var bins = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            bins.Add((await host.NewBinAsync()).Id);
        }

        var random = new Random(42);
        for (var i = 0; i < 80; i++)
        {
            var post = new StockPost(
                variants[random.Next(variants.Count)],
                random.Next(2) == 0 ? Grades.Accepted : Grades.Concession,
                bins[random.Next(bins.Count)],
                random.Next(-5, 9) is var q && q == 0 ? 1 : q,
                MovementKinds.Adjustment,
                1);
            try
            {
                await host.SendAsync(new TestPostCommand([post]));
            }
            catch (DomainException)
            {
                // a removal that would go negative is refused; the books must still agree
            }
        }

        var mismatches = await host.SendAsync(new StockConsistencyQuery());
        Assert.DoesNotContain(mismatches, m => variants.Contains(m.VariantRefId));
    }

    [Fact]
    [Trait("Spec", "LED-1")]
    [Trait("Spec", "NFR-SC-05")]
    public async Task TheDatabase_RefusesToUpdateOrDeleteALedgerRow()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 5)]));
        await using var db = postgres.NewContext();

        var update = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("""UPDATE "WHSE"."STCK_MVMT" SET "QTY_CHNG" = 99, "AFT_QTY" = 99 WHERE "VRNT_REF_ID" = {0};""", variant.RefId));
        var delete = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("""DELETE FROM "WHSE"."STCK_MVMT" WHERE "VRNT_REF_ID" = {0};""", variant.RefId));

        Assert.Contains("append-only", update.MessageText, StringComparison.Ordinal);
        Assert.Contains("append-only", delete.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "LED-3")]
    public async Task TheDatabase_RefusesANegativeBalance_EvenIfTheCodeIsBypassed()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        await host.SendAsync(new TestPostCommand([Put(variant.RefId, bin.Id, 2)]));
        await using var db = postgres.NewContext();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("""UPDATE "WHSE"."STCK_BAL" SET "QTY" = -1 WHERE "VRNT_REF_ID" = {0};""", variant.RefId));

        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    [Trait("Spec", "LED-10")]
    public async Task ThePostingPort_TakesNoTotalsFromTheCaller_OnlyASignedChange()
    {
        // The StockPost type has no balance or total field at all, so a caller cannot even express one; the balance is whatever the SQL says.
        var properties = typeof(StockPost).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(properties, n => n.Contains("Balance", StringComparison.Ordinal) || n.Contains("Total", StringComparison.Ordinal) || n.Contains("After", StringComparison.Ordinal));
        await Task.CompletedTask;
    }
}
