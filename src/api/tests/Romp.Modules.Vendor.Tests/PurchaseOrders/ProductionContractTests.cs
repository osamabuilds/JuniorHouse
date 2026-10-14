using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Revisions;
using Romp.Modules.Vendor.Contracts;
using Romp.Modules.Vendor.Domain.Revisions;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.PurchaseOrders;

/// <summary>Sprint 3 (ADR 0008, D11, D12): the Vendor.Contracts questions PROD asks, and VNDR's consumers of PROD's events.</summary>
public sealed class ProductionContractTests
{
    private static readonly DateTimeOffset SentAt = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AmendedAt = new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);

    /// <summary>A Sent PO with Rev 0 (cost 500) and an immediately-in-force Rev 1 (cost 600), with their timestamps pinned to known days.</summary>
    private static async Task<(IServiceProvider Provider, long PoId)> AmendedPoAsync()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "cost up", null, 600m, po.ExpectedDeliveryDate.AddDays(10), po.LatestAcceptableDate!.Value.AddDays(10),
            po.OverTolerancePercent, po.UnderTolerancePercent, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        var db = provider.GetRequiredService<VendorDbContext>();
        var sent = await db.PurchaseOrders.Where(p => p.Id == po.Id).SelectMany(p => p.StatusHistory).SingleAsync(h => h.PoStatusId == 2);
        sent.InsrDte = SentAt;
        foreach (var history in await db.Set<PoRevisionStatusHistoryEntry>().Where(h => h.ToStatusId == 2).ToListAsync())
        {
            var isRevisionZero = await db.Set<PurchaseOrderRevision>().AnyAsync(r => r.Id == history.RevisionId && r.RevisionNumber == 0);
            history.InsrDte = isRevisionZero ? SentAt : AmendedAt;
        }

        await db.SaveChangesAsync();
        return (provider, po.Id);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task GetProductionContext_ReportsSendDateInPakistanTime_AndOnlyWhatProdNeeds()
    {
        var (provider, poId) = await AmendedPoAsync();
        var queries = provider.GetRequiredService<IPurchaseOrderQueries>();

        var context = await queries.GetProductionContextAsync(poId, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal(new DateOnly(2026, 9, 1), context.SentDate); // 08:00 UTC is 13:00 in Pakistan: same day
        Assert.Equal((short?)1, context.FabricResponsibilityId);
        Assert.StartsWith("PO-", context.PoNo);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task GetProductionContext_LateEveningUtcIsTheNextDayInPakistan()
    {
        var (provider, poId) = await AmendedPoAsync();
        var db = provider.GetRequiredService<VendorDbContext>();
        var sent = await db.PurchaseOrders.Where(p => p.Id == poId).SelectMany(p => p.StatusHistory).SingleAsync(h => h.PoStatusId == 2);
        sent.InsrDte = new DateTimeOffset(2026, 9, 1, 20, 30, 0, TimeSpan.Zero); // 01:30 on 2 Sep in Pakistan
        await db.SaveChangesAsync();

        var context = await provider.GetRequiredService<IPurchaseOrderQueries>().GetProductionContextAsync(poId, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 9, 2), context!.SentDate);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task GetProductionContext_DraftOrUnknown_Null()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var po = await PoTestHelpers.CreateDraftPoAsync(provider.GetRequiredService<ISender>());
        var queries = provider.GetRequiredService<IPurchaseOrderQueries>();

        Assert.Null(await queries.GetProductionContextAsync(po.Id, CancellationToken.None));
        Assert.Null(await queries.GetProductionContextAsync(999999, CancellationToken.None));
    }

    [Fact]
    [Trait("Spec", "AC-23")]
    public async Task GetTermsAt_AfterAmendment_ReturnsRevisionInForceOnThatDate()
    {
        var (provider, poId) = await AmendedPoAsync();
        var queries = provider.GetRequiredService<IPurchaseOrderQueries>();

        var before = await queries.GetTermsAtAsync(poId, new DateOnly(2026, 9, 10), CancellationToken.None);
        var onTheDay = await queries.GetTermsAtAsync(poId, new DateOnly(2026, 9, 20), CancellationToken.None); // amended that day: in force at end of day
        var after = await queries.GetTermsAtAsync(poId, new DateOnly(2026, 10, 5), CancellationToken.None);
        var dayBefore = await queries.GetTermsAtAsync(poId, new DateOnly(2026, 9, 19), CancellationToken.None);

        Assert.Equal((short)0, before!.RevisionNumber);
        Assert.Equal(500m, before.UnitCost);
        Assert.Equal((short)0, dayBefore!.RevisionNumber);
        Assert.Equal((short)1, onTheDay!.RevisionNumber);
        Assert.Equal((short)1, after!.RevisionNumber);
        Assert.Equal(600m, after.UnitCost);
        Assert.True(after.ExpectedDeliveryDate > before.ExpectedDeliveryDate); // the dates moved with the revision
        Assert.Equal(before.Lines.Count, after.Lines.Count);
    }

    [Fact]
    [Trait("Spec", "AC-23")]
    public async Task GetTermsAt_BackfilledRevisionZero_UsesSendDate()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        // A backfilled Rev 0 has no recorded put-in-force time: drop its status history and ask for any date.
        var db = provider.GetRequiredService<VendorDbContext>();
        db.Set<PoRevisionStatusHistoryEntry>().RemoveRange(await db.Set<PoRevisionStatusHistoryEntry>().ToListAsync());
        await db.SaveChangesAsync();

        var queries = provider.GetRequiredService<IPurchaseOrderQueries>();
        Assert.Equal((short)0, (await queries.GetTermsAtAsync(po.Id, new DateOnly(2026, 1, 1), CancellationToken.None))!.RevisionNumber);
        Assert.Equal((short)0, (await queries.GetTermsAtAsync(po.Id, new DateOnly(2030, 1, 1), CancellationToken.None))!.RevisionNumber);
    }

    [Fact]
    [Trait("Spec", "AC-23")]
    public async Task GetTermsAt_DateBeforeSend_FallsBackToRevisionZero_AndUnknownPoIsNull()
    {
        var (provider, poId) = await AmendedPoAsync();
        var queries = provider.GetRequiredService<IPurchaseOrderQueries>();

        Assert.Equal((short)0, (await queries.GetTermsAtAsync(poId, new DateOnly(2026, 1, 1), CancellationToken.None))!.RevisionNumber);
        Assert.Null(await queries.GetTermsAtAsync(999999, new DateOnly(2026, 9, 10), CancellationToken.None));
    }

    // ---- VNDR's consumers of PROD's events ------------------------------------------------------------------------

    private static async Task DeliverAsync<TEvent>(IServiceProvider provider, long messageId, TEvent domainEvent, string consumerName)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxMessageContext>();
        context.MessageId = messageId;
        context.Schema = "PROD";
        var consumerType = typeof(VendorDbContext).Assembly.GetTypes().Single(t => t.Name == consumerName);
        var consumer = (IOutboxMessageHandler<TEvent>)ActivatorUtilities.CreateInstance(scope.ServiceProvider, consumerType);
        await consumer.HandleAsync(domainEvent, CancellationToken.None);
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task ProductionStarted_TwiceSameMessage_OneTransition()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PurchaseOrderLifecycleTests.AcknowledgedPoAsync(sender);
        var started = new ProductionStartedEvent(1, po.Id, po.PoNo, new DateOnly(2026, 9, 10));

        await DeliverAsync(provider, 77, started, "ProductionStartedConsumer");
        await DeliverAsync(provider, 77, started, "ProductionStartedConsumer");

        var db = provider.GetRequiredService<VendorDbContext>();
        Assert.Equal(5, (await sender.Send(new GetPurchaseOrderByIdQuery(po.Id)))!.StatusId);
        Assert.Equal(1, await db.Set<OutboxMessage>().CountAsync(m => m.EventType == "PoProductionStartedEvent"));
        Assert.Equal(1, await db.Set<InboxMessage>().CountAsync());
    }

    [Theory]
    [Trait("Spec", "AC-24")]
    [InlineData("PartiallyDelivered", 6)]
    [InlineData("Delivered", 7)]
    public async Task DeliveryRecorded_AppliesPodOutcome(string outcome, short expectedStatus)
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PurchaseOrderLifecycleTests.AcknowledgedPoAsync(sender);

        await DeliverAsync(provider, 5, DeliveryRecorded(po, outcome), "DeliveryNoteRecordedConsumer");

        Assert.Equal(expectedStatus, (await sender.Send(new GetPurchaseOrderByIdQuery(po.Id)))!.StatusId);
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    public async Task DeliveryRecorded_OutOfOrder_NoRegression()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PurchaseOrderLifecycleTests.AcknowledgedPoAsync(sender);

        await DeliverAsync(provider, 2, DeliveryRecorded(po, "Delivered"), "DeliveryNoteRecordedConsumer");
        await DeliverAsync(provider, 1, DeliveryRecorded(po, "PartiallyDelivered"), "DeliveryNoteRecordedConsumer"); // older message arrives late

        Assert.Equal(7, (await sender.Send(new GetPurchaseOrderByIdQuery(po.Id)))!.StatusId);
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    public async Task DeliveryRecorded_PartiallyThenDelivered_Advances()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PurchaseOrderLifecycleTests.AcknowledgedPoAsync(sender);

        await DeliverAsync(provider, 1, DeliveryRecorded(po, "PartiallyDelivered"), "DeliveryNoteRecordedConsumer");
        await DeliverAsync(provider, 2, DeliveryRecorded(po, "Delivered"), "DeliveryNoteRecordedConsumer");

        Assert.Equal(7, (await sender.Send(new GetPurchaseOrderByIdQuery(po.Id)))!.StatusId);
    }

    private static DeliveryNoteRecordedEvent DeliveryRecorded(PoDto po, string outcome) => new(
        1, po.Id, po.PoNo, "DN-1", new DateOnly(2026, 9, 25), false, outcome, "OnTime", 0, "OnQuantity", 0, 0, 0, false, []);
}
