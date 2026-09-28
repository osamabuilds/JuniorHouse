using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Tests;

public sealed class UpdatePurchaseOrderCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task Handle_DraftPo_SavesChanges()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        var updated = await sender.Send(new UpdatePurchaseOrderCommand(
            po.Id, UnitCost: 600m, ExpectedDeliveryDate: new DateOnly(2027, 1, 15),
            PaymentTermId: 5, AdvancePercent: 20m, Lines: [new PoLineDto(1, 10, 200)]));

        Assert.Equal(600m, updated.UnitCost);
        Assert.Equal(new DateOnly(2027, 1, 15), updated.ExpectedDeliveryDate);
        Assert.Single(updated.Lines);
        Assert.Equal(200, updated.Lines.Single().Qty);
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task Handle_AcknowledgedPo_RejectedWithDomainError()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Domain.DomainException>(() => sender.Send(
            new UpdatePurchaseOrderCommand(po.Id, 700m, new DateOnly(2027, 2, 1), 1, 10m, [new PoLineDto(1, 10, 10)])));
    }
}
