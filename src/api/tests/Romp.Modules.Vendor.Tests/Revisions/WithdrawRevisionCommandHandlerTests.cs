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

public sealed class WithdrawRevisionCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task Handle_PendingRevision_BecomesWithdrawn()
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

        var reloadedPo = await sender.Send(new WithdrawRevisionCommand(po.Id, pendingRevision.RevisionNumber, Note: "Buyer changed mind"));

        Assert.Equal(po.UnitCost, reloadedPo.UnitCost); // unchanged - a withdrawn revision never takes effect

        var dbContext = provider.GetRequiredService<VendorDbContext>();
        var revision = await dbContext.Set<Domain.Revisions.PurchaseOrderRevision>()
            .SingleAsync(r => r.PoId == po.Id && r.RevisionNumber == pendingRevision.RevisionNumber);
        Assert.Equal(5, revision.StatusId); // RevisionStatus.Withdrawn
    }
}
