using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Warehouse.Application.Bins;
using Romp.Modules.Warehouse.Application.Putaway;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Putaway;
using Romp.Modules.Warehouse.Infrastructure.Persistence;
using Romp.Modules.Warehouse.Tests.Support;

namespace Romp.Modules.Warehouse.Tests.Putaway;

[Collection(WarehousePostgresFixtureDefinition.Name)]
public sealed class PutawayFlowTests(WarehousePostgres postgres)
{
    private const string Person = "Ayesha Khan";

    private static long _seed = 50_000;

    /// <summary>One QC-accepted sub-batch for the variant's style/colour/size, delivered through the real mapper and command.</summary>
    private async Task<long> AcceptAsync(
        WarehouseHost host, (long RefId, long VariantId, string Gtin, string Sku) variant, int qty, string outcome = "Accepted", long? subBatchId = null)
    {
        var next = Interlocked.Increment(ref _seed);
        var poId = next;
        host.PoStyles.StyleByPo[poId] = 500 + variant.VariantId; // NewVariantAsync uses style 500 + variant id, colour 10, size 1
        var message = new QcBatchAcceptedEvent(next, subBatchId ?? next, next, poId, $"PO-{next}", outcome, [new QcLine(1, 10, qty)]);
        var mapped = PutawayQcMapper.Map(message, 500);
        Assert.True(mapped.IsValid, mapped.Rejection);
        await host.SendAsync(new RecordAcceptedBatchCommand(mapped.Batch!));
        await using var db = postgres.NewContext();
        return (await db.PutawayItems.AsNoTracking().SingleAsync(i => i.SubBatchId == (subBatchId ?? next))).Id;
    }

    private static ConfirmPutawayCommand Confirm(long itemId, string bin, int qty, string? gtin, Guid? key = null) =>
        new(itemId, bin, qty, Scanned: gtin is not null, gtin, key ?? Guid.NewGuid(), Person);

    [Fact]
    [Trait("Spec", "PUT-1")]
    [Trait("Spec", "PUT-3")]
    [Trait("Spec", "TC-PUT-01")]
    public async Task QcAcceptance_CreatesAPendingItem_ButNoStockIsAvailableUntilPutaway()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();

        var itemId = await AcceptAsync(host, variant, 12);

        var row = Assert.Single((await host.SendAsync(new SearchPutawayItemsQuery())).Items, i => i.Id == itemId);
        Assert.Equal((12, 0, 12, "PENDING", "ACCEPTED"), (row.QcQuantity, row.PutQuantity, row.Pending, row.StatusCode, row.GradeCode));
        Assert.Empty((await host.SendAsync(new SearchStockQuery(variant.Sku))).Items);
        Assert.Equal(0, await host.InScopeAsync(sp => sp.GetService<IStockVariantReader>()!.GetOnHandAsync(variant.RefId, default)));
    }

    [Fact]
    [Trait("Spec", "PUT-2")]
    public async Task ARedeliveredMessage_DoesNotDoubleThePendingQuantity()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var subBatch = Interlocked.Increment(ref _seed);

        var itemId = await AcceptAsync(host, variant, 12, subBatchId: subBatch);
        var again = await AcceptAsync(host, variant, 12, subBatchId: subBatch);

        Assert.Equal(itemId, again);
        Assert.Equal(12, (await host.SendAsync(new SearchPutawayItemsQuery())).Items.Single(i => i.Id == itemId).Pending);
    }

    [Fact]
    [Trait("Spec", "PUT-2")]
    public async Task AnItemCreatedBeforeTheGtinExists_WaitsAndIsLinkedWhenTheVariantArrives_InEitherOrder()
    {
        using var host = new WarehouseHost(postgres);
        var n = Interlocked.Increment(ref _seed);
        var styleId = 900_000 + n;
        var poId = n;
        host.PoStyles.StyleByPo[poId] = styleId;
        var subBatch = n;
        var message = new QcBatchAcceptedEvent(n, subBatch, n, poId, "PO-X", "Accepted", [new QcLine(1, 10, 5)]);
        await host.SendAsync(new RecordAcceptedBatchCommand(PutawayQcMapper.Map(message, 500).Batch!));

        await using (var db = postgres.NewContext())
        {
            var waiting = await db.PutawayItems.AsNoTracking().SingleAsync(i => i.SubBatchId == subBatch);
            Assert.Equal(PutawayStatuses.AwaitingGtin, waiting.StatusId);
        }

        var gtin = Gtin13.WithCheckDigit("3" + n.ToString("00000000000", System.Globalization.CultureInfo.InvariantCulture));
        await host.SendAsync(new RecordVariantAssignedCommand(n, styleId, 10, 1, $"ROMP-LATE-{n}", gtin));
        Assert.Equal(1, await host.SendAsync(new LinkVariantToItemsCommand(styleId, 10, 1)));
        Assert.Equal(0, await host.SendAsync(new LinkVariantToItemsCommand(styleId, 10, 1)));

        await using var after = postgres.NewContext();
        Assert.Equal(PutawayStatuses.Pending, (await after.PutawayItems.AsNoTracking().SingleAsync(i => i.SubBatchId == subBatch)).StatusId);
    }

    [Theory]
    [InlineData("Rejected")]
    [InlineData("Rework")]
    [InlineData("Pending")]
    [InlineData("")]
    [Trait("Spec", "PUT-12")]
    public void OnlyReleasedOutcomes_ReachThePutawayQueue(string outcome)
    {
        var message = new QcBatchAcceptedEvent(1, 2, 3, 4, "PO-1", outcome, [new QcLine(1, 10, 5)]);

        var mapped = PutawayQcMapper.Map(message, 500);

        Assert.False(mapped.IsValid);
        Assert.Contains("released", mapped.Rejection, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 2, 3)]
    [InlineData(1, 0, 3)]
    [InlineData(1, 2, 0)]
    [Trait("Spec", "PUT-12")]
    public void AMessageWithNonPositiveIds_IsRefused(long batch, long subBatch, long po) =>
        Assert.False(PutawayQcMapper.Map(new QcBatchAcceptedEvent(batch, subBatch, 1, po, "PO", "Accepted", [new QcLine(1, 10, 5)]), 500).IsValid);

    [Fact]
    [Trait("Spec", "PUT-12")]
    public void MalformedLines_AreRefused_BeforeAnythingIsStored()
    {
        QcBatchAcceptedEvent Message(params QcLine[] lines) => new(1, 2, 1, 4, "PO", "Accepted", lines);

        Assert.False(PutawayQcMapper.Map(Message(), 500).IsValid);
        Assert.False(PutawayQcMapper.Map(Message(new QcLine(1, 10, 0)), 500).IsValid);
        Assert.False(PutawayQcMapper.Map(Message(new QcLine(0, 10, 3)), 500).IsValid);
        Assert.False(PutawayQcMapper.Map(Message(new QcLine(1, 10, 3), new QcLine(1, 10, 4)), 500).IsValid);
        Assert.False(PutawayQcMapper.Map(Message(new QcLine(1, 10, 3), new QcLine(2, 10, 4)), 1).IsValid);
        Assert.True(PutawayQcMapper.Map(Message(new QcLine(1, 10, 3), new QcLine(2, 10, 4)), 500).IsValid);
    }

    [Fact]
    [Trait("Spec", "PUT-4")]
    [Trait("Spec", "PUT-10")]
    [Trait("Spec", "PUT-15")]
    [Trait("Spec", "TC-PUT-02")]
    [Trait("Spec", "TC-PUT-04")]
    public async Task Confirming_MovesTheBalance_TheLedger_TheRecordAndTheEvents_InOneTransaction()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        var itemId = await AcceptAsync(host, variant, 10);

        var record = await host.SendAsync(Confirm(itemId, bin.BinCode.ToLowerInvariant(), 4, variant.Gtin));

        Assert.Equal((4, 6, true, Person), (record.Quantity, record.ItemPending, record.Scanned, record.ActorName));
        Assert.Equal(4, await host.InScopeAsync(sp => sp.GetService<IStockVariantReader>()!.GetOnHandAsync(variant.RefId, default)));
        await using var db = postgres.NewContext();
        var movement = await db.Movements.AsNoTracking().SingleAsync(m => m.VariantRefId == variant.RefId);
        Assert.Equal((MovementKinds.Putaway, itemId, 4, 0, 4), (movement.KindId, movement.SourceId, movement.QuantityChange, movement.BalanceBefore, movement.BalanceAfter));
        var stored = await db.PutawayRecords.AsNoTracking().SingleAsync(r => r.ItemId == itemId);
        Assert.NotEqual(default, stored.InsrDte);
        Assert.Equal(1, db.Set<OutboxMessage>().Where(m => m.EventType == "StockPutawayConfirmedEvent").AsEnumerable().Count(m => m.Payload.Contains(variant.Sku, StringComparison.Ordinal)));
        Assert.Equal(1, db.Set<OutboxMessage>().Where(m => m.EventType == "StockLevelChangedEvent").AsEnumerable().Count(m => m.Payload.Contains(variant.Sku, StringComparison.Ordinal)));
    }

    [Fact]
    [Trait("Spec", "PUT-5")]
    public async Task PartialPutaway_AcrossTwoBins_ThenTheRestCompletesTheItem_AndMoreIsRefused()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var binA = await host.NewBinAsync();
        var binB = await host.NewBinAsync();
        var itemId = await AcceptAsync(host, variant, 10);

        await host.SendAsync(Confirm(itemId, binA.BinCode, 7, variant.Gtin));
        var last = await host.SendAsync(Confirm(itemId, binB.BinCode, 3, variant.Gtin));

        Assert.Equal(0, last.ItemPending);
        Assert.Equal("COMPLETE", (await host.SendAsync(new SearchPutawayItemsQuery())).Items.Single(i => i.Id == itemId).StatusCode);
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Confirm(itemId, binB.BinCode, 1, variant.Gtin)));
        var stock = Assert.Single((await host.SendAsync(new SearchStockQuery(variant.Sku))).Items);
        Assert.Equal(2, stock.Bins.Count);
        Assert.Equal(10, stock.OnHand);
    }

    [Fact]
    [Trait("Spec", "PUT-21")]
    [Trait("Spec", "PUT-5")]
    public async Task MoreThanIsPending_IsRefused_PointingAtAFoundStockAdjustment_AndNothingChanges()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        var itemId = await AcceptAsync(host, variant, 5);

        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Confirm(itemId, bin.BinCode, 6, variant.Gtin)));

        Assert.Contains("FOUND_STOCK", ex.Message, StringComparison.Ordinal);
        Assert.Empty((await host.SendAsync(new SearchStockQuery(variant.Sku))).Items);
        await using var db = postgres.NewContext();
        Assert.False(await db.PutawayRecords.AnyAsync(r => r.ItemId == itemId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(100_001)]
    [Trait("Spec", "PUT-5")]
    public async Task AnInvalidQuantity_IsA400StyleValidationError(int qty)
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        var itemId = await AcceptAsync(host, variant, 5);

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Confirm(itemId, bin.BinCode, qty, variant.Gtin)));
    }

    [Fact]
    [Trait("Spec", "PUT-6")]
    [Trait("Spec", "TC-PUT-03")]
    public async Task AWrongGtin_AnUnknownBin_AndAnInactiveBin_AreRefused_EachSayingWhich()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var other = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        var off = await host.NewBinAsync();
        await host.SendAsync(new SetBinActiveCommand(off.Id, false, Person));
        var itemId = await AcceptAsync(host, variant, 5);

        var wrong = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Confirm(itemId, bin.BinCode, 1, other.Gtin)));
        var unknown = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Confirm(itemId, "NO-1-X", 1, variant.Gtin)));
        var inactive = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Confirm(itemId, off.BinCode, 1, variant.Gtin)));
        var invalid = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(Confirm(itemId, bin.BinCode, 1, "2000000000017")));

        Assert.Contains("does not belong", wrong.Message, StringComparison.Ordinal);
        Assert.Contains("does not exist", unknown.Message, StringComparison.Ordinal);
        Assert.Contains("inactive", inactive.Message, StringComparison.Ordinal);
        Assert.NotNull(invalid);
        Assert.Empty((await host.SendAsync(new SearchStockQuery(variant.Sku))).Items);
    }

    [Fact]
    [Trait("Spec", "PUT-7")]
    public async Task ARetiredGtin_IsRefusedAtPutaway_AndShownAsRetiredInTheQueueAndTheScan()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        var itemId = await AcceptAsync(host, variant, 5);
        await host.SendAsync(new RecordVariantVoidedCommand(variant.VariantId, variant.Gtin));

        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Confirm(itemId, bin.BinCode, 1, variant.Gtin)));

        Assert.Contains("retired", ex.Message, StringComparison.Ordinal);
        Assert.True((await host.SendAsync(new SearchPutawayItemsQuery())).Items.Single(i => i.Id == itemId).GtinRetired);
        Assert.True((await host.SendAsync(new ScanLookupQuery(variant.Gtin)))!.GtinRetired);
    }

    [Fact]
    [Trait("Spec", "PUT-8")]
    public async Task TheSameKeyTwice_AppliesOnce_AndReturnsTheOriginalRecord()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        var itemId = await AcceptAsync(host, variant, 10);
        var key = Guid.NewGuid();

        var first = await host.SendAsync(Confirm(itemId, bin.BinCode, 4, variant.Gtin, key));
        var second = await host.SendAsync(Confirm(itemId, bin.BinCode, 4, variant.Gtin, key));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(4, await host.InScopeAsync(sp => sp.GetService<IStockVariantReader>()!.GetOnHandAsync(variant.RefId, default)));
        Assert.Equal(6, (await host.SendAsync(new SearchPutawayItemsQuery())).Items.Single(i => i.Id == itemId).Pending);
    }

    [Fact]
    [Trait("Spec", "PUT-9")]
    public async Task TwentyParallelConfirmsOfOneUnit_AgainstTenPending_ApplyExactlyTen()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        var itemId = await AcceptAsync(host, variant, 10);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            try
            {
                await host.SendAsync(Confirm(itemId, bin.BinCode, 1, variant.Gtin));
                return true;
            }
            catch (DomainException)
            {
                return false;
            }
        }));

        Assert.Equal(10, results.Count(applied => applied));
        Assert.Equal(10, await host.InScopeAsync(sp => sp.GetService<IStockVariantReader>()!.GetOnHandAsync(variant.RefId, default)));
        await using var db = postgres.NewContext();
        Assert.Equal(10, (await db.PutawayItems.AsNoTracking().SingleAsync(i => i.Id == itemId)).PutQuantity);
    }

    [Fact]
    [Trait("Spec", "PUT-19")]
    public async Task AnItemChosenFromTheList_CanBePutAwayWithoutAGtinScan()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        var itemId = await AcceptAsync(host, variant, 5);

        var record = await host.SendAsync(Confirm(itemId, bin.BinCode, 5, gtin: null));

        Assert.False(record.Scanned);
        Assert.Equal(5, await host.InScopeAsync(sp => sp.GetService<IStockVariantReader>()!.GetOnHandAsync(variant.RefId, default)));
    }

    [Fact]
    [Trait("Spec", "PUT-20")]
    public async Task ConcessionStock_IsShelvedInItsOwnGrade_AndNeverMergedWithAccepted()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var bin = await host.NewBinAsync();
        var accepted = await AcceptAsync(host, variant, 4);
        var concession = await AcceptAsync(host, variant, 3, outcome: "Concession");

        await host.SendAsync(Confirm(accepted, bin.BinCode, 4, variant.Gtin));
        await host.SendAsync(Confirm(concession, bin.BinCode, 3, variant.Gtin));

        var row = Assert.Single((await host.SendAsync(new SearchStockQuery(variant.Sku))).Items);
        Assert.Equal((4, 3, 7), (row.OnHandAccepted, row.OnHandConcession, row.OnHand));
        Assert.Equal(2, row.Bins.Count);
    }

    [Fact]
    [Trait("Spec", "PUT-2")]
    public async Task AnItemStillAwaitingItsGtin_CannotBePutAway()
    {
        using var host = new WarehouseHost(postgres);
        var n = Interlocked.Increment(ref _seed);
        host.PoStyles.StyleByPo[n] = 800_000 + n;
        await host.SendAsync(new RecordAcceptedBatchCommand(PutawayQcMapper.Map(
            new QcBatchAcceptedEvent(n, n, n, n, "PO", "Accepted", [new QcLine(1, 10, 3)]), 500).Batch!));
        var bin = await host.NewBinAsync();
        long itemId;
        await using (var db = postgres.NewContext())
        {
            itemId = (await db.PutawayItems.AsNoTracking().SingleAsync(i => i.SubBatchId == n)).Id;
        }

        var ex = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Confirm(itemId, bin.BinCode, 1, gtin: null)));

        Assert.Contains("waiting for its GTIN", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "PUT-6")]
    public async Task TheScanLookup_KnowsTheBarcode_ListsItsPendingItems_AndRefusesAMalformedOne()
    {
        using var host = new WarehouseHost(postgres);
        var variant = await host.NewVariantAsync();
        var itemId = await AcceptAsync(host, variant, 5);

        var found = await host.SendAsync(new ScanLookupQuery(variant.Gtin));
        var unknown = await host.SendAsync(new ScanLookupQuery(Gtin13.WithCheckDigit("399999999999")));

        Assert.NotNull(found);
        Assert.Equal(variant.Sku, found.SkuCode);
        Assert.Equal(itemId, Assert.Single(found.PendingItems).ItemId);
        Assert.Null(unknown);
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ScanLookupQuery("123")));
    }

    [Fact]
    [Trait("Spec", "PUT-14")]
    public async Task TheQueueAndHistory_ArePaged_OldestFirst_AndRunAConstantNumberOfQueries()
    {
        var counter = new CommandCounter();
        using var host = new WarehouseHost(postgres, counter: counter);
        var bin = await host.NewBinAsync();
        var variant = await host.NewVariantAsync();
        var firstItem = await AcceptAsync(host, variant, 5);
        await host.SendAsync(Confirm(firstItem, bin.BinCode, 1, variant.Gtin));

        counter.Reset();
        await host.SendAsync(new SearchPutawayItemsQuery(Page: 1, PageSize: 5));
        await host.SendAsync(new SearchPutawayRecordsQuery(Page: 1, PageSize: 5));
        var small = counter.Count;

        for (var i = 0; i < 8; i++)
        {
            var v = await host.NewVariantAsync();
            var item = await AcceptAsync(host, v, 2);
            await host.SendAsync(Confirm(item, bin.BinCode, 1, v.Gtin));
        }

        counter.Reset();
        var items = await host.SendAsync(new SearchPutawayItemsQuery(Page: 1, PageSize: 100));
        var records = await host.SendAsync(new SearchPutawayRecordsQuery(Page: 1, PageSize: 100));

        Assert.Equal(small, counter.Count);
        Assert.True(items.Items.Count >= 9 && records.Items.Count >= 9);
        Assert.True(items.Items.Zip(items.Items.Skip(1)).All(p => p.First.Id < p.Second.Id));
        Assert.True(records.Items.Zip(records.Items.Skip(1)).All(p => p.First.Id > p.Second.Id));
        Assert.Equal(100, (await host.SendAsync(new SearchPutawayItemsQuery(PageSize: 10_000))).PageSize);
        _ = PageRequest.DefaultPageSize;
    }

    [Fact]
    [Trait("Spec", "PUT-19")]
    [Trait("Spec", "PUT-14")]
    public async Task TheQueue_CanBeSearchedBySkuOrExactBarcode_SoOneItemIsFoundAmongMany()
    {
        using var host = new WarehouseHost(postgres);
        var wanted = await host.NewVariantAsync();
        var other = await host.NewVariantAsync();
        var wantedItem = await AcceptAsync(host, wanted, 5);
        await AcceptAsync(host, other, 5);

        var bySku = await host.SendAsync(new SearchPutawayItemsQuery(Search: wanted.Sku.ToLowerInvariant(), PageSize: 100));
        var byGtin = await host.SendAsync(new SearchPutawayItemsQuery(Search: wanted.Gtin, PageSize: 100));
        var nothing = await host.SendAsync(new SearchPutawayItemsQuery(Search: "NO-SUCH-SKU", PageSize: 100));

        Assert.Equal(wantedItem, Assert.Single(bySku.Items).Id);
        Assert.Equal(wantedItem, Assert.Single(byGtin.Items).Id);
        Assert.Empty(nothing.Items);
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new SearchPutawayItemsQuery(Search: new string('x', 51))));
    }

    [Fact]
    [Trait("Spec", "PUT-13")]
    public async Task APutawayRecord_HasNoUpdatePath_AMistakeIsAnAdjustmentNotAnEdit()
    {
        // The record type exposes no mutators: every property has a private setter apart from the audit columns.
        var mutable = typeof(PutawayRecord).GetProperties()
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .Where(n => n is not ("InsrDte" or "InsrBy" or "UpdtDte" or "UpdtBy"))
            .ToList();
        Assert.Empty(mutable);
        await Task.CompletedTask;
    }

    [Fact]
    [Trait("Spec", "PUT-1")]
    public async Task APoWithoutAStyle_IsAnErrorTheDispatcherWillRetry_NotSilentlyDropped()
    {
        using var host = new WarehouseHost(postgres);
        var message = new QcBatchAcceptedEvent(1, 2, 3, 987_654_321, "PO", "Accepted", [new QcLine(1, 10, 5)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.SendAsync(new RecordAcceptedBatchCommand(PutawayQcMapper.Map(message, 500).Batch!)));
    }
}
