using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Revisions;
using Romp.Modules.Vendor.Domain.Revisions;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.Revisions;

public sealed class DecideRevisionCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task Handle_Accept_RevisionInForceMirrorUpdated()
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

        var reloadedPo = await sender.Send(new DecideRevisionCommand(po.Id, pendingRevision.RevisionNumber, Accept: true, Note: null));

        Assert.Equal(600m, reloadedPo.UnitCost);

        var dbContext = provider.GetRequiredService<VendorDbContext>();
        var revision = await dbContext.Set<Domain.Revisions.PurchaseOrderRevision>()
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
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var pendingRevision = await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "Cost increase", null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        var reloadedPo = await sender.Send(new DecideRevisionCommand(po.Id, pendingRevision.RevisionNumber, Accept: false, Note: "Not agreed"));

        Assert.Equal(po.UnitCost, reloadedPo.UnitCost); // unchanged - the rejected revision never took effect

        var dbContext = provider.GetRequiredService<VendorDbContext>();
        var revision = await dbContext.Set<Domain.Revisions.PurchaseOrderRevision>()
            .SingleAsync(r => r.PoId == po.Id && r.RevisionNumber == pendingRevision.RevisionNumber);
        Assert.Equal(4, revision.StatusId); // RevisionStatus.Rejected - still visible/queryable in history
    }

    /// <summary>
    /// Regression: the other tests share one tracked DbContext across calls, which hides a handler
    /// that forgets to load a revision's lines. A fresh scope per command behaves like real requests.
    /// </summary>
    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task Handle_Accept_FreshContext_CarriesRevisionLinesOntoPo()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        async Task<T> InNewScope<T>(Func<ISender, Task<T>> action)
        {
            using var scope = provider.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<ISender>());
        }

        var po = await InNewScope(s => PoTestHelpers.CreateDraftPoAsync(s));
        await InNewScope(s => s.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true)));
        await InNewScope(s => s.Send(new AcknowledgePurchaseOrderCommand(po.Id)));
        var newLines = new[] { new PoLineDto(1, 10, 300), new PoLineDto(2, 20, 25) };
        var revision = await InNewScope(s => s.Send(new CreateAmendmentCommand(
            po.Id, 1, 6, "Size mix change", null,
            po.UnitCost, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, newLines)));

        var accepted = await InNewScope(s => s.Send(new DecideRevisionCommand(po.Id, revision.RevisionNumber, Accept: true, Note: null)));

        Assert.Equal(newLines.OrderBy(l => l.SizeId), accepted.Lines.OrderBy(l => l.SizeId));
    }
}
