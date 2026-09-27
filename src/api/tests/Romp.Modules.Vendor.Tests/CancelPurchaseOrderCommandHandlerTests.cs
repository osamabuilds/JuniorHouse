using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Infrastructure;

namespace Romp.Modules.Vendor.Tests;

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
        Assert.Contains(dbContext.Set<Romp.BuildingBlocks.Persistence.OutboxMessage>(), m => m.EventType == "PoCancelledEvent");
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
        await sender.Send(new SendPurchaseOrderCommand(po.Id));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var cancelled = await sender.Send(new CancelPurchaseOrderCommand(po.Id, CancelReasonId: 1));

        Assert.Equal(4, cancelled.StatusId);
    }
}
