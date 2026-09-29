using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Revisions;
using Romp.Modules.Vendor.Domain.Revisions;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.PurchaseOrders;

public sealed class CancelPurchaseOrderCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Handle_ValidReason_CancelsAndWritesHistoryAndOutboxEvent()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        var cancelled = await sender.Send(new CancelPurchaseOrderCommand(po.Id, CancelReasonId: 2));

        Assert.Equal(4, cancelled.StatusId); // PoStatus.Cancelled
        Assert.Equal((short)2, cancelled.StatusHistory.Last().CancelReasonId);

        var dbContext = provider.GetRequiredService<VendorDbContext>();
        Assert.Contains(dbContext.Set<Romp.BuildingBlocks.Persistence.Outbox.OutboxMessage>(), m => m.EventType == "PoCancelledEvent");
    }

    [Fact]
    [Trait("Spec", "AC-12a")]
    public async Task Handle_MissingReason_RejectedWithValidationError()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(new CancelPurchaseOrderCommand(po.Id, CancelReasonId: 0)));

        Assert.Contains(nameof(CancelPurchaseOrderCommand.CancelReasonId), exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Handle_AcknowledgedPo_CanStillBeCancelled()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var cancelled = await sender.Send(new CancelPurchaseOrderCommand(po.Id, CancelReasonId: 1));

        Assert.Equal(4, cancelled.StatusId);
    }

    [Fact]
    [Trait("Spec", "AC-21")]
    public async Task Handle_PoWithPendingRevision_AutoWithdrawsRevision()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var pendingRevision = await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "Cost increase", null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        var cancelled = await sender.Send(new CancelPurchaseOrderCommand(po.Id, CancelReasonId: 1));

        Assert.Equal(4, cancelled.StatusId); // PoStatus.Cancelled

        var dbContext = provider.GetRequiredService<VendorDbContext>();
        var revision = await dbContext.Set<Domain.Revisions.PurchaseOrderRevision>()
            .SingleAsync(r => r.PoId == po.Id && r.RevisionNumber == pendingRevision.RevisionNumber);
        Assert.Equal(5, revision.StatusId); // RevisionStatus.Withdrawn, in the same Cancel transaction
    }
}
