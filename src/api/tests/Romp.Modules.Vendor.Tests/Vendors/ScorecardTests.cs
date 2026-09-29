using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Vendors;
using Romp.Modules.Vendor.Contracts;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.Vendors;

public sealed class ScorecardCalculatorTests
{
    [Fact]
    [Trait("Spec", "AC-37")]
    public void Calculate_NoData_AllNull_NeverZeroOrHundred()
    {
        var measures = ScorecardCalculator.Calculate(new ScorecardCounts(0, 0, 0, 0, 0, 0));

        Assert.Equal(new ScorecardMeasures(null, null, null), measures);
    }

    [Fact]
    [Trait("Spec", "AC-35")]
    public void Calculate_AllOnTimeAndOnQuantity_HundredPercent_NoDefects_ZeroPercent()
    {
        var measures = ScorecardCalculator.Calculate(new ScorecardCounts(4, 4, 4, 0, 200, 0));

        Assert.Equal(new ScorecardMeasures(100m, 100m, 0m), measures);
    }

    [Fact]
    [Trait("Spec", "AC-35")]
    public void Calculate_Mixed_RoundsToOneDecimal()
    {
        // 2 of 3 on time (66.7), 1 of 3 on quantity (33.3), 7 of 80 defective (8.8).
        var measures = ScorecardCalculator.Calculate(new ScorecardCounts(3, 2, 1, 0, 80, 7));

        Assert.Equal(new ScorecardMeasures(66.7m, 33.3m, 8.8m), measures);
    }

    [Fact]
    [Trait("Spec", "AC-36")]
    public void Calculate_RompDelayedDeliveries_LeftOutOfOnTimeOnly()
    {
        // 4 deliveries; 1 was late because Romp shipped the fabric late (excluded); of the other 3, 2 were on time.
        var measures = ScorecardCalculator.Calculate(new ScorecardCounts(4, 2, 4, 1, 0, 0));

        Assert.Equal(66.7m, measures.OnTimePct);
        Assert.Equal(100m, measures.OnQuantityPct); // quantity still counts every delivery
        Assert.Null(measures.DefectRatePct);
    }

    [Fact]
    [Trait("Spec", "AC-36")]
    public void Calculate_EveryDeliveryExcluded_OnTimeIsNoData()
    {
        Assert.Null(ScorecardCalculator.Calculate(new ScorecardCounts(2, 0, 2, 2, 0, 0)).OnTimePct);
    }
}

public sealed class ScorecardFactTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static IServiceProvider Host(string name) => TestServices.Build(name, s => s.AddSingleton<TimeProvider>(new FixedClock(Now)));

    private static async Task<(long VendorId, long PoId)> SentPoAsync(IServiceProvider provider)
    {
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        return (po.VendorId, po.Id);
    }

    private static Task<bool> DeliverAsync(IServiceProvider provider, long poId, string dn, DateOnly dispatch, string timing = "OnTime", string quantity = "OnQuantity", bool rompDelay = false) =>
        provider.GetRequiredService<ISender>().Send(new RecordScorecardDeliveryCommand(poId, dn, dispatch, timing, quantity, rompDelay));

    [Fact]
    [Trait("Spec", "AC-37")]
    public async Task Handle_NoFacts_ReturnsNulls()
    {
        var provider = Host(nameof(Handle_NoFacts_ReturnsNulls));
        var (vendorId, _) = await SentPoAsync(provider);

        var scorecard = await provider.GetRequiredService<ISender>().Send(new GetVendorScorecardQuery(vendorId));

        Assert.Equal((0, 0, 0), (scorecard.DeliveriesCounted, scorecard.InspectedUnits, scorecard.DefectiveUnits));
        Assert.Null(scorecard.OnTimePct);
        Assert.Null(scorecard.OnQuantityPct);
        Assert.Null(scorecard.DefectRatePct);
        Assert.Null(scorecard.OnTimePctRolling);
        Assert.Equal(90, scorecard.WindowDays);
    }

    [Fact]
    [Trait("Spec", "AC-35")]
    public async Task Handle_Facts_ProduceMeasuresWithTheirCounts()
    {
        var provider = Host(nameof(Handle_Facts_ProduceMeasuresWithTheirCounts));
        var (vendorId, poId) = await SentPoAsync(provider);
        await DeliverAsync(provider, poId, "DN-1", new DateOnly(2026, 9, 20));
        await DeliverAsync(provider, poId, "DN-2", new DateOnly(2026, 9, 25), timing: "Late", quantity: "Short");
        await provider.GetRequiredService<ISender>().Send(new RecordScorecardInspectionCommand(poId, 11, 0, 1, 80, 4, Now));

        var scorecard = await provider.GetRequiredService<ISender>().Send(new GetVendorScorecardQuery(vendorId));

        Assert.Equal((2, 1, 1, 0), (scorecard.DeliveriesCounted, scorecard.DeliveriesOnTime, scorecard.DeliveriesOnQuantity, scorecard.DeliveriesExcludedRompDelay));
        Assert.Equal((80, 4), (scorecard.InspectedUnits, scorecard.DefectiveUnits));
        Assert.Equal((50m, 50m, 5m), (scorecard.OnTimePct, scorecard.OnQuantityPct, scorecard.DefectRatePct));
    }

    [Fact]
    [Trait("Spec", "AC-36")]
    public async Task Handle_RompFabricDelay_ExcludedFromOnTime_AndShownAsACount()
    {
        var provider = Host(nameof(Handle_RompFabricDelay_ExcludedFromOnTime_AndShownAsACount));
        var (vendorId, poId) = await SentPoAsync(provider);
        await DeliverAsync(provider, poId, "DN-1", new DateOnly(2026, 9, 20));
        await DeliverAsync(provider, poId, "DN-2", new DateOnly(2026, 9, 25), timing: "Late", rompDelay: true);

        var scorecard = await provider.GetRequiredService<ISender>().Send(new GetVendorScorecardQuery(vendorId));

        Assert.Equal(1, scorecard.DeliveriesExcludedRompDelay);
        Assert.Equal(100m, scorecard.OnTimePct); // the Romp-caused late delivery does not count against the vendor
    }

    [Fact]
    [Trait("Spec", "AC-35")]
    public async Task Handle_RollingWindow_CutsOffOldFacts()
    {
        var provider = Host(nameof(Handle_RollingWindow_CutsOffOldFacts));
        var (vendorId, poId) = await SentPoAsync(provider);
        await DeliverAsync(provider, poId, "DN-OLD", new DateOnly(2026, 3, 1), timing: "Late", quantity: "Short"); // 212 days ago
        await DeliverAsync(provider, poId, "DN-NEW", new DateOnly(2026, 9, 20)); // 9 days ago

        var scorecard = await provider.GetRequiredService<ISender>().Send(new GetVendorScorecardQuery(vendorId));

        Assert.Equal(50m, scorecard.OnTimePct); // all time: 1 of 2
        Assert.Equal(100m, scorecard.OnTimePctRolling); // last 90 days: only the recent, on-time one
        Assert.Equal(100m, scorecard.OnQuantityPctRolling);
    }

    [Fact]
    [Trait("Spec", "AC-38")]
    public async Task DeliveryNote_TwiceSameMessage_CountedOnce()
    {
        var provider = Host(nameof(DeliveryNote_TwiceSameMessage_CountedOnce));
        var (vendorId, poId) = await SentPoAsync(provider);

        var first = await DeliverAsync(provider, poId, "DN-1", new DateOnly(2026, 9, 20));
        var second = await DeliverAsync(provider, poId, "DN-1", new DateOnly(2026, 9, 20));

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, (await provider.GetRequiredService<ISender>().Send(new GetVendorScorecardQuery(vendorId))).DeliveriesCounted);
    }

    [Fact]
    [Trait("Spec", "AC-38")]
    public async Task Inspection_TwiceSameMessage_CountedOnce_AndOnlyTheFirstPassCounts()
    {
        var provider = Host(nameof(Inspection_TwiceSameMessage_CountedOnce_AndOnlyTheFirstPassCounts));
        var (vendorId, poId) = await SentPoAsync(provider);
        var sender = provider.GetRequiredService<ISender>();

        Assert.True(await sender.Send(new RecordScorecardInspectionCommand(poId, 11, 0, 1, 80, 4, Now)));
        Assert.False(await sender.Send(new RecordScorecardInspectionCommand(poId, 11, 0, 1, 80, 4, Now)));
        Assert.False(await sender.Send(new RecordScorecardInspectionCommand(poId, 11, 77, 2, 30, 0, Now))); // a rework re-inspection

        var scorecard = await sender.Send(new GetVendorScorecardQuery(vendorId));
        Assert.Equal((80, 4), (scorecard.InspectedUnits, scorecard.DefectiveUnits));
    }

    [Fact]
    [Trait("Spec", "AC-38")]
    public async Task Facts_ForAnUnknownPo_AreIgnored()
    {
        var provider = Host(nameof(Facts_ForAnUnknownPo_AreIgnored));

        Assert.False(await DeliverAsync(provider, 9999, "DN-1", new DateOnly(2026, 9, 20)));
        Assert.False(await provider.GetRequiredService<ISender>().Send(new RecordScorecardInspectionCommand(9999, 1, 0, 1, 10, 0, Now)));
    }

    [Fact]
    [Trait("Spec", "AC-35")]
    public async Task Handle_UnknownVendor_NotFound()
    {
        var provider = Host(nameof(Handle_UnknownVendor_NotFound));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => provider.GetRequiredService<ISender>().Send(new GetVendorScorecardQuery(4242)));
    }

    [Fact]
    [Trait("Spec", "AC-39")]
    public async Task Scorecard_ReadOnlyContract_ReturnsTheSameNumbers_AndNullForUnknown()
    {
        var provider = Host(nameof(Scorecard_ReadOnlyContract_ReturnsTheSameNumbers_AndNullForUnknown));
        var (vendorId, poId) = await SentPoAsync(provider);
        await DeliverAsync(provider, poId, "DN-1", new DateOnly(2026, 9, 20));
        var queries = new Romp.Modules.Vendor.Infrastructure.Vendors.VendorScorecardQueries(provider.GetRequiredService<ISender>());

        var scorecard = await queries.GetScorecardAsync(vendorId, CancellationToken.None);

        Assert.Equal(100m, scorecard!.OnTimePct);
        Assert.Null(await queries.GetScorecardAsync(4242, CancellationToken.None));
        Assert.DoesNotContain(typeof(IVendorScorecardQueries).GetMethods(), m => m.Name.StartsWith("Set", StringComparison.Ordinal) || m.Name.StartsWith("Update", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "AC-45")]
    public void Scorecard_NeverCarriesNotesCostsOrEvidence()
    {
        var names = typeof(VendorScorecard).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(names, n => n.Contains("Note", StringComparison.OrdinalIgnoreCase) || n.Contains("Cost", StringComparison.OrdinalIgnoreCase) || n.Contains("Evidence", StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class ReceiptProgressTests
{
    private static async Task<(IServiceProvider Provider, long PoId)> AcknowledgedPoAsync(string name)
    {
        var provider = TestServices.Build(name);
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        return (provider, po.Id);
    }

    private static ApplyGoodsReceiptRecordedCommand Receipt(long poId, long grnId, params ReceiptLineInput[] lines) =>
        new(poId, grnId, $"GRN-2026-{grnId:D5}", new DateOnly(2026, 9, 25), lines);

    [Fact]
    [Trait("Spec", "AC-40")]
    public async Task Progress_SeveralGrns_SumsPerSizeColour()
    {
        var (provider, poId) = await AcknowledgedPoAsync(nameof(Progress_SeveralGrns_SumsPerSizeColour));
        var sender = provider.GetRequiredService<ISender>();
        await sender.Send(Receipt(poId, 1, new ReceiptLineInput(1, 10, 60, 58), new ReceiptLineInput(2, 20, 30, 30)));
        await sender.Send(Receipt(poId, 2, new ReceiptLineInput(1, 10, 40, 40)));

        var progress = await sender.Send(new GetPoReceiptProgressQuery(poId));

        Assert.Equal((150, 128), (progress.OrderedTotal, progress.ReceivedTotal));
        var line1 = progress.Lines.Single(l => l.SizeId == 1);
        Assert.Equal((100, 98, 2), (line1.OrderedQty, line1.ReceivedQty, line1.OutstandingQty));
        var line2 = progress.Lines.Single(l => l.SizeId == 2);
        Assert.Equal((50, 30, 20), (line2.OrderedQty, line2.ReceivedQty, line2.OutstandingQty));
        Assert.Equal(["GRN-2026-00001", "GRN-2026-00002"], progress.Receipts.Select(r => r.GrnNo).ToArray());
    }

    [Fact]
    [Trait("Spec", "AC-40")]
    public async Task Progress_OverReceived_OutstandingNeverNegative()
    {
        var (provider, poId) = await AcknowledgedPoAsync(nameof(Progress_OverReceived_OutstandingNeverNegative));
        var sender = provider.GetRequiredService<ISender>();
        await sender.Send(Receipt(poId, 1, new ReceiptLineInput(1, 10, 110, 110)));

        var line = (await sender.Send(new GetPoReceiptProgressQuery(poId))).Lines.Single(l => l.SizeId == 1);

        Assert.Equal((100, 110, 0), (line.OrderedQty, line.ReceivedQty, line.OutstandingQty));
    }

    [Fact]
    [Trait("Spec", "AC-40")]
    public async Task Progress_UnknownPo_NotFound()
    {
        var provider = TestServices.Build(nameof(Progress_UnknownPo_NotFound));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => provider.GetRequiredService<ISender>().Send(new GetPoReceiptProgressQuery(4242)));
    }

    [Fact]
    [Trait("Spec", "AC-41")]
    public async Task Recorded_ChangesNoPoStatus()
    {
        var (provider, poId) = await AcknowledgedPoAsync(nameof(Recorded_ChangesNoPoStatus));
        var db = provider.GetRequiredService<VendorDbContext>();
        var before = (await db.PurchaseOrders.AsNoTracking().SingleAsync(p => p.Id == poId)).StatusId;

        await provider.GetRequiredService<ISender>().Send(Receipt(poId, 1, new ReceiptLineInput(1, 10, 100, 100), new ReceiptLineInput(2, 20, 50, 50)));

        Assert.Equal(before, (await db.PurchaseOrders.AsNoTracking().SingleAsync(p => p.Id == poId)).StatusId);
        Assert.Equal(0, await db.Set<OutboxMessage>().CountAsync(m => m.EventType.StartsWith("Po") && m.EventType != "PoCreatedEvent" && m.EventType != "PoSentToVendorEvent"));
    }

    [Fact]
    [Trait("Spec", "AC-41")]
    public async Task Recorded_TwiceSameGrn_CountedOnce()
    {
        var (provider, poId) = await AcknowledgedPoAsync(nameof(Recorded_TwiceSameGrn_CountedOnce));
        var sender = provider.GetRequiredService<ISender>();

        Assert.True(await sender.Send(Receipt(poId, 1, new ReceiptLineInput(1, 10, 100, 100))));
        Assert.False(await sender.Send(Receipt(poId, 1, new ReceiptLineInput(1, 10, 100, 100))));
        Assert.False(await sender.Send(Receipt(9999, 2, new ReceiptLineInput(1, 10, 1, 1)))); // unknown PO

        Assert.Equal(100, (await sender.Send(new GetPoReceiptProgressQuery(poId))).ReceivedTotal);
    }

    private static readonly Type[] InfrastructureTypes = typeof(VendorDbContext).Assembly.GetTypes();

    private static async Task ConsumeAsync<TEvent>(IServiceProvider provider, string consumerName, long messageId, TEvent domainEvent)
        where TEvent : class, Romp.BuildingBlocks.Domain.IDomainEvent
    {
        await using var scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<OutboxMessageContext>().MessageId = messageId;
        scope.ServiceProvider.GetRequiredService<OutboxMessageContext>().Schema = "QC";
        var type = InfrastructureTypes.Single(t => t.Name == consumerName);
        var consumer = (IOutboxMessageHandler<TEvent>)ActivatorUtilities.CreateInstance(scope.ServiceProvider, type);
        await consumer.HandleAsync(domainEvent, CancellationToken.None);
    }

    [Fact]
    [Trait("Spec", "AC-41")]
    public async Task Consumer_TwiceSameMessage_OneReceiptAndOneInboxRow()
    {
        var (provider, poId) = await AcknowledgedPoAsync(nameof(Consumer_TwiceSameMessage_OneReceiptAndOneInboxRow));
        var domainEvent = new GoodsReceiptRecordedEvent(
            7, "GRN-2026-00007", 1, poId, "PO-X", "DN-1", new DateOnly(2026, 9, 25), "Short", 100, 90, [new GrnEventLine(1, 10, 100, 90)]);

        await ConsumeAsync(provider, "GoodsReceiptRecordedConsumer", 501, domainEvent);
        await ConsumeAsync(provider, "GoodsReceiptRecordedConsumer", 501, domainEvent);

        var db = provider.GetRequiredService<VendorDbContext>();
        Assert.Equal(1, await db.PoReceipts.CountAsync());
        Assert.Equal(1, await db.Set<InboxMessage>().CountAsync(m => m.MessageId == 501));
    }

    [Fact]
    [Trait("Spec", "AC-38")]
    public async Task Consumers_ForDeliveryAndInspection_AreIdempotentThroughTheInbox()
    {
        var (provider, poId) = await AcknowledgedPoAsync(nameof(Consumers_ForDeliveryAndInspection_AreIdempotentThroughTheInbox));
        var delivery = new DeliveryNoteRecordedEvent(
            1, poId, "PO-X", "DN-1", new DateOnly(2026, 9, 20), false, "PartiallyDelivered", "OnTime", 0, "OnQuantity", 0, 0, 0, false,
            [new DeliveredLine(1, 10, 50)]);
        var inspection = new QcInspectionRecordedEvent(3, 0, 1, poId, "PO-X", 1, "Aql", 100, 20, 1, 0, 0, 1, "Pass");

        for (var i = 0; i < 2; i++)
        {
            await ConsumeAsync(provider, "ScorecardDeliveryConsumer", 601, delivery);
            await ConsumeAsync(provider, "ScorecardInspectionConsumer", 602, inspection);
        }

        var db = provider.GetRequiredService<VendorDbContext>();
        Assert.Equal(1, await db.ScorecardDeliveries.CountAsync());
        Assert.Equal(1, await db.ScorecardInspections.CountAsync());
    }
}
