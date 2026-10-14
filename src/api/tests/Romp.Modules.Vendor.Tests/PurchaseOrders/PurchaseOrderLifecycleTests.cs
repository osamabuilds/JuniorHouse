using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.PurchaseOrders;

/// <summary>Sprint 3 (ADR 0008): the PO's lifecycle after Acknowledged - In Production, Partially Delivered, Delivered, Closed.</summary>
public sealed class PurchaseOrderLifecycleTests
{
    private const short InProduction = 5;
    private const short PartiallyDelivered = 6;
    private const short Delivered = 7;
    private const short Closed = 8;
    private const short ProductionAlreadyStarted = 7; // PO_CNCL_RSN_LKP id

    internal static async Task<PoDto> AcknowledgedPoAsync(ISender sender)
    {
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        return await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));
    }

    private static async Task<short> StatusAsync(ISender sender, long poId) => (await sender.Send(new GetPurchaseOrderByIdQuery(poId)))!.StatusId;

    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task Transitions_ForwardOnly_IdempotentAndRaiseEvents()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await AcknowledgedPoAsync(sender);

        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Started));
        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Started)); // redelivered
        Assert.Equal(InProduction, await StatusAsync(sender, po.Id));

        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.PartiallyDelivered));
        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Started)); // stale, out of order
        Assert.Equal(PartiallyDelivered, await StatusAsync(sender, po.Id));

        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Delivered));
        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.PartiallyDelivered)); // stale
        Assert.Equal(Delivered, await StatusAsync(sender, po.Id));

        var events = await provider.GetRequiredService<VendorDbContext>().Set<OutboxMessage>().Select(m => m.EventType).ToListAsync();
        Assert.Equal(1, events.Count(e => e == "PoProductionStartedEvent"));
        Assert.Equal(1, events.Count(e => e == "PoPartiallyDeliveredEvent"));
        Assert.Equal(1, events.Count(e => e == "PoDeliveredEvent"));

        // §5.9: every transition is in the status history, in order, once.
        var history = (await sender.Send(new GetPurchaseOrderByIdQuery(po.Id)))!.StatusHistory.Select(h => h.PoStatusId).ToArray();
        Assert.Equal([1, 2, 3, InProduction, PartiallyDelivered, Delivered], history);
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task Start_DoesNotHappenAtAcknowledged_OnlyWhenProductionReports()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();

        var po = await AcknowledgedPoAsync(sender);

        Assert.Equal(3, po.StatusId); // D1: In Production fires at bulk cutting, not at Acknowledged
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task Apply_OnAPoThatWasNeverAcknowledged_Refused()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        await Assert.ThrowsAsync<DomainException>(() => sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Started)));
    }

    [Fact]
    [Trait("Spec", "AC-29")]
    public async Task Apply_OnACancelledPo_IsIgnored()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await AcknowledgedPoAsync(sender);
        await sender.Send(new CancelPurchaseOrderCommand(po.Id, 1));

        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Started));

        Assert.Equal(4, await StatusAsync(sender, po.Id));
    }

    [Theory]
    [Trait("Spec", "AC-28")]
    [InlineData(InProduction)]
    [InlineData(PartiallyDelivered)]
    public async Task Handle_InProduction_CancelsWithReason(short statusBefore)
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await AcknowledgedPoAsync(sender);
        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Started));
        if (statusBefore == PartiallyDelivered)
        {
            await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.PartiallyDelivered));
        }

        var cancelled = await sender.Send(new CancelPurchaseOrderCommand(po.Id, ProductionAlreadyStarted));

        Assert.Equal(4, cancelled.StatusId);
        Assert.Equal((short?)ProductionAlreadyStarted, cancelled.StatusHistory.Last().CancelReasonId);
    }

    [Theory]
    [Trait("Spec", "AC-28")]
    [InlineData(Delivered)]
    [InlineData(Closed)]
    public async Task Handle_DeliveredOrClosed_Refused(short statusBefore)
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await AcknowledgedPoAsync(sender);
        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Delivered));
        if (statusBefore == Closed)
        {
            await sender.Send(new ClosePurchaseOrderCommand(po.Id));
        }

        var error = await Assert.ThrowsAsync<DomainException>(() => sender.Send(new CancelPurchaseOrderCommand(po.Id, 1)));

        Assert.Contains("can no longer be cancelled", error.Message);
        Assert.Equal(statusBefore, await StatusAsync(sender, po.Id));
    }

    [Fact]
    [Trait("Spec", "AC-25")]
    public async Task Handle_Delivered_Closes()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await AcknowledgedPoAsync(sender);
        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Delivered));

        var closed = await sender.Send(new ClosePurchaseOrderCommand(po.Id));

        Assert.Equal(Closed, closed.StatusId);
        Assert.Equal(Closed, closed.StatusHistory.Last().PoStatusId);
        Assert.False(string.IsNullOrEmpty(closed.StatusHistory.Last().InsrBy)); // who closed it (system actor until S8)
        Assert.Contains(await provider.GetRequiredService<VendorDbContext>().Set<OutboxMessage>().Select(m => m.EventType).ToListAsync(), e => e == "PoClosedEvent");
    }

    [Theory]
    [Trait("Spec", "AC-25")]
    [InlineData(0)] // Acknowledged
    [InlineData(1)] // In Production
    [InlineData(2)] // Partially Delivered
    public async Task Handle_NotDelivered_Refused_NeverAutomatic(int stage)
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await AcknowledgedPoAsync(sender);
        if (stage >= 1)
        {
            await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Started));
        }

        if (stage >= 2)
        {
            await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.PartiallyDelivered));
        }

        var error = await Assert.ThrowsAsync<DomainException>(() => sender.Send(new ClosePurchaseOrderCommand(po.Id)));

        Assert.Contains("once it is Delivered", error.Message);
    }

    [Fact]
    [Trait("Spec", "AC-25")]
    public async Task Close_UnknownPo_NotFound()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sender.Send(new ClosePurchaseOrderCommand(424242)));
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    public async Task Delivered_PoCanNoLongerBeEditedOrReAcknowledged()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await AcknowledgedPoAsync(sender);
        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Delivered));

        await Assert.ThrowsAsync<DomainException>(() => sender.Send(new AcknowledgePurchaseOrderCommand(po.Id)));
    }
}
