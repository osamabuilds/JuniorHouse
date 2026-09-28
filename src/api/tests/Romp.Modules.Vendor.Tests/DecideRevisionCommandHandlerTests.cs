using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Infrastructure;

namespace Romp.Modules.Vendor.Tests;

public sealed class DecideRevisionCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task Handle_Accept_RevisionInForceMirrorUpdated()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var pendingRevision = await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "Cost increase", null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        var reloadedPo = await sender.Send(new DecideRevisionCommand(po.Id, pendingRevision.RevisionNumber, Accept: true, Note: null));

        Assert.Equal(600m, reloadedPo.UnitCost);

        var dbContext = provider.GetRequiredService<VendorDbContext>();
        var revision = await dbContext.Set<Domain.PurchaseOrderRevision>()
            .SingleAsync(r => r.PoId == po.Id && r.RevisionNumber == pendingRevision.RevisionNumber);
        Assert.Equal(2, revision.StatusId); // RevisionStatus.InForce
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Handle_Reject_PositionUnchangedRevisionVisibleInHistory()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var pendingRevision = await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "Cost increase", null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        var reloadedPo = await sender.Send(new DecideRevisionCommand(po.Id, pendingRevision.RevisionNumber, Accept: false, Note: "Not agreed"));

        Assert.Equal(po.UnitCost, reloadedPo.UnitCost); // unchanged - the rejected revision never took effect

        var dbContext = provider.GetRequiredService<VendorDbContext>();
        var revision = await dbContext.Set<Domain.PurchaseOrderRevision>()
            .SingleAsync(r => r.PoId == po.Id && r.RevisionNumber == pendingRevision.RevisionNumber);
        Assert.Equal(4, revision.StatusId); // RevisionStatus.Rejected - still visible/queryable in history
    }
}
