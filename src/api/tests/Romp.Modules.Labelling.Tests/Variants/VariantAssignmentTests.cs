using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.LabelWork;
using Romp.Modules.Labelling.Application.Variants;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.Variants;
using Romp.Modules.Labelling.Infrastructure.Persistence;
using Romp.Modules.Labelling.Tests.Support;

namespace Romp.Modules.Labelling.Tests.Variants;

[Collection(LabellingPostgresFixtureDefinition.Name)]
public sealed class VariantAssignmentTests(LabellingPostgres postgres)
{
    private static long OutboxCount(SkuDbContext db, string eventType, string contains) =>
        db.Set<OutboxMessage>().Where(m => m.EventType == eventType).AsEnumerable().Count(m => m.Payload.Contains(contains, StringComparison.Ordinal));

    [Fact]
    [Trait("Spec", "VAR-1")]
    [Trait("Spec", "VAR-7")]
    [Trait("Spec", "VAR-10")]
    public async Task AcceptedBatch_GivesEachVariantAValidGtinAndSku_AndWritesTheEventsInTheSameTransaction()
    {
        using var host = new LabellingHost(postgres);
        var (poId, styleId) = host.NewStyle("STY-ROMPER");

        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 5), (2, 11, 7))));

        await using var db = postgres.NewContext();
        var variants = await db.Variants.Include(v => v.Gtins).Where(v => v.StyleId == styleId).OrderBy(v => v.Id).ToListAsync();
        Assert.Equal(2, variants.Count);
        Assert.Equal("ROMP-STY-ROMPER-RED-2-3Y", variants[0].SkuCode);
        Assert.Equal("ROMP-STY-ROMPER-BLUE-4-5Y", variants[1].SkuCode);
        foreach (var variant in variants)
        {
            var gtin = Assert.Single(variant.Gtins);
            Assert.Equal(GtinStatus.Active, gtin.StatusId);
            Assert.True(Gtin.TryParse(gtin.Gtin, out _), "valid GTIN-13 with a correct check digit");
            Assert.StartsWith("0200000", gtin.Gtin, StringComparison.Ordinal);
            Assert.Equal(1, OutboxCount(db, "VariantGtinAssignedEvent", variant.SkuCode));
        }

        Assert.NotEqual(variants[0].Gtins.Single().Gtin, variants[1].Gtins.Single().Gtin);
    }

    [Fact]
    [Trait("Spec", "VAR-2")]
    public async Task ALaterDeliveryOfTheSameVariant_ReusesTheGtin_AndTakesNoNewNumber()
    {
        using var host = new LabellingHost(postgres);
        var (poId, styleId) = host.NewStyle();
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 5))));
        var counterBefore = await host.InScopeAsync(sp => sp.GetRequiredService<IGtinSequence>().CurrentAsync("0200000", default));

        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Concession, poId, (1, 10, 3))));

        await using var db = postgres.NewContext();
        var variant = await db.Variants.Include(v => v.Gtins).SingleAsync(v => v.StyleId == styleId);
        Assert.Single(variant.Gtins);
        var counterAfter = await host.InScopeAsync(sp => sp.GetRequiredService<IGtinSequence>().CurrentAsync("0200000", default));
        Assert.Equal(counterBefore, counterAfter);
        Assert.Equal(1, OutboxCount(db, "VariantGtinAssignedEvent", variant.SkuCode));
    }

    [Fact]
    [Trait("Spec", "VAR-4")]
    [Trait("Spec", "VAR-6")]
    public async Task ManyAssignmentsAtOnce_AllGetDistinctGtinsAndItemReferences()
    {
        using var host = new LabellingHost(postgres);
        var batches = Enumerable.Range(0, 40).Select(_ =>
        {
            var (poId, _) = host.NewStyle();
            return Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 1));
        }).ToList();

        await Task.WhenAll(batches.Select(b => host.SendAsync(new RecordAcceptedBatchCommand(b))));

        await using var db = postgres.NewContext();
        var styleIds = host.Lookup.StyleByPo.Where(p => batches.Any(b => b.PoId == p.Key)).Select(p => p.Value!.Value).ToList();
        var gtins = await db.Variants.Where(v => styleIds.Contains(v.StyleId)).SelectMany(v => v.Gtins).ToListAsync();
        Assert.Equal(40, gtins.Count);
        Assert.Equal(40, gtins.Select(g => g.Gtin).Distinct().Count());
        Assert.Equal(40, gtins.Select(g => g.ItemReference).Distinct().Count());
    }

    [Fact]
    [Trait("Spec", "VAR-3")]
    public async Task TheSameVariantRacedByManyBatches_EndsWithExactlyOneVariantAndOneActiveGtin()
    {
        using var host = new LabellingHost(postgres);
        var (poId, styleId) = host.NewStyle();

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            try
            {
                await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 1))));
                return (Exception?)null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }));

        // A loser of the race fails on the unique index; the outbox dispatcher would retry its message. Nothing else may fail.
        Assert.All(results.OfType<Exception>(), ex => Assert.True(ex is DbUpdateException or InvalidOperationException, ex.ToString()));
        await using var db = postgres.NewContext();
        var variant = await db.Variants.Include(v => v.Gtins).SingleAsync(v => v.StyleId == styleId);
        Assert.Single(variant.Gtins, g => g.StatusId == GtinStatus.Active);
    }

    [Fact]
    [Trait("Spec", "VAR-6")]
    public async Task ARolledBackAssignment_ReleasesItsNumber_SoThereAreNoGaps()
    {
        using var host = new LabellingHost(postgres);
        const string prefix = "0210001";

        long first;
        await using (var scope = host.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SkuDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            first = await scope.ServiceProvider.GetRequiredService<IGtinSequence>().NextAsync(prefix, default);
            await transaction.RollbackAsync();
        }

        var second = await host.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<SkuDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var value = await sp.GetRequiredService<IGtinSequence>().NextAsync(prefix, default);
            await transaction.CommitAsync();
            return value;
        });

        Assert.Equal(1, first);
        Assert.Equal(first, second);
    }

    [Fact]
    [Trait("Spec", "VAR-3")]
    public async Task TheDatabaseItself_RefusesASecondActiveGtinForOneVariant()
    {
        using var host = new LabellingHost(postgres);
        var (poId, styleId) = host.NewStyle();
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 1))));
        await using var db = postgres.NewContext();
        var variantId = await db.Variants.Where(v => v.StyleId == styleId).Select(v => v.Id).SingleAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "SKU"."VRNT_GTIN" ("VRNT_ID","GTIN","PFX","ITEM_REF","GTIN_STS_ID","INSR_DTE","INSR_BY")
            VALUES ({0}, '0200000999991', '0200000', 999991, 1, now(), 'test');
            """,
            variantId));
        Assert.Equal("23505", ex.SqlState);
    }

    [Fact]
    [Trait("Spec", "VAR-5")]
    [Trait("Spec", "VAR-8")]
    [Trait("Spec", "VAR-9")]
    public async Task VoidingRetiresTheNumberForGood_AndTheNextAcceptedQuantityGetsANewOne()
    {
        using var host = new LabellingHost(postgres);
        var (poId, styleId) = host.NewStyle();
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 4))));
        var variantId = await host.InScopeAsync(sp => sp.GetRequiredService<SkuDbContext>().Variants.Where(v => v.StyleId == styleId).Select(v => v.Id).SingleAsync());

        var detail = await host.SendAsync(new VoidGtinCommand(variantId, VoidReasons.Error, "wrong colour on the tag", "Asha"));

        Assert.Null(detail.Variant.ActiveGtin);
        var retired = Assert.Single(detail.Gtins);
        Assert.Equal("Retired", retired.StatusCode);
        Assert.Equal("Error", retired.RetiredReasonCode);
        Assert.Equal("Asha", retired.RetiredBy);
        await using (var db = postgres.NewContext())
        {
            Assert.Equal(1, OutboxCount(db, "GtinVoidedEvent", retired.Gtin));
        }

        // The next accepted quantity for the variant: same SKU, brand-new number, and the old one is not reused.
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 2))));
        var after = await host.SendAsync(new GetVariantQuery(variantId));
        Assert.Equal(2, after.Gtins.Count);
        Assert.NotNull(after.Variant.ActiveGtin);
        Assert.NotEqual(retired.Gtin, after.Variant.ActiveGtin);
        Assert.Equal(detail.Variant.SkuCode, after.Variant.SkuCode);
    }

    [Fact]
    [Trait("Spec", "VAR-8")]
    public async Task Void_NeedsAReasonAndAPerson_AndAnActiveGtin()
    {
        using var host = new LabellingHost(postgres);
        var (poId, styleId) = host.NewStyle();
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 4))));
        var variantId = await host.InScopeAsync(sp => sp.GetRequiredService<SkuDbContext>().Variants.Where(v => v.StyleId == styleId).Select(v => v.Id).SingleAsync());

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => host.SendAsync(new VoidGtinCommand(variantId, 99, null, "Asha")));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => host.SendAsync(new VoidGtinCommand(variantId, VoidReasons.Error, null, " ")));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => host.SendAsync(new VoidGtinCommand(variantId, VoidReasons.Error, "bad\nnote", "Asha")));
        await host.SendAsync(new VoidGtinCommand(variantId, VoidReasons.Other, null, "Asha"));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new VoidGtinCommand(variantId, VoidReasons.Other, null, "Asha"))); // nothing active to void
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new VoidGtinCommand(long.MaxValue, VoidReasons.Other, null, "Asha")));
    }

    [Fact]
    [Trait("Spec", "VAR-12")]
    public async Task WhenTheRangeIsUsedUp_AssignmentFailsCleanly_AndTakesNothing()
    {
        // A 9-digit prefix leaves 3 digits: 1,000 numbers. Put the counter at the last valid reference.
        using var host = new LabellingHost(postgres, o => o.CompanyPrefix = "021000009");
        await using (var db = postgres.NewContext())
        {
            await db.Database.ExecuteSqlRawAsync("""INSERT INTO "SKU"."GTIN_NO_SEQ" ("PFX","SEQ") VALUES ('021000009', 999);""");
        }

        var (poId, styleId) = host.NewStyle();
        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, (1, 10, 1)))));

        Assert.Contains("used up", ex.Message, StringComparison.Ordinal);
        await using var check = postgres.NewContext();
        Assert.False(await check.Variants.AnyAsync(v => v.StyleId == styleId));
        var capacity = await host.SendAsync(new GetGtinCapacityQuery());
        Assert.Equal(999, capacity.Used);
        Assert.Equal(0, capacity.Remaining);
        Assert.True(capacity.Warning);
        Assert.True(capacity.IsTestPrefix);
    }

    [Fact]
    [Trait("Spec", "VAR-13")]
    public async Task TheRegister_IsPaged_Ordered_SearchableAndRunsAConstantNumberOfQueries()
    {
        var counter = new CommandCounter();
        using var host = new LabellingHost(postgres, counter: counter);
        var (poId, styleId) = host.NewStyle("STY-REG");
        var sizes = new (short Size, short Colour, int Qty)[] { (1, 10, 1), (2, 10, 1), (3, 10, 1), (1, 11, 1), (2, 11, 1), (3, 11, 1) };
        await host.SendAsync(new RecordAcceptedBatchCommand(Batches.Batch(LabelOutcome.Accepted, poId, sizes)));

        counter.Reset();
        var page1 = await host.SendAsync(new SearchVariantsQuery("STY-REG", null, 1, 4));
        var queries = counter.Count;
        var page2 = await host.SendAsync(new SearchVariantsQuery("STY-REG", null, 2, 4));

        Assert.Equal(6, page1.TotalCount);
        Assert.Equal(4, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Empty(page1.Items.Select(i => i.Id).Intersect(page2.Items.Select(i => i.Id)));
        Assert.True(page1.Items.Zip(page1.Items.Skip(1)).All(p => p.First.Id > p.Second.Id), "ordered by id descending");
        Assert.True(queries <= 3, $"a page of the register must not run per row: {queries} queries");
        Assert.All(page1.Items, i => Assert.Equal("Style " + styleId, i.StyleName));

        var byGtin = await host.SendAsync(new SearchVariantsQuery(page1.Items[0].ActiveGtin, null));
        Assert.Equal(page1.Items[0].Id, Assert.Single(byGtin.Items).Id);
        var clamped = await host.SendAsync(new SearchVariantsQuery("STY-REG", null, 1, 5000));
        Assert.Equal(PageRequest.MaxPageSize, clamped.PageSize);
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => host.SendAsync(new SearchVariantsQuery(new string('x', 51))));
    }
}
