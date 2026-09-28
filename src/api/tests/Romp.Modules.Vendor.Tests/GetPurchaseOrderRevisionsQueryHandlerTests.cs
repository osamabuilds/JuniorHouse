using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Tests;

public sealed class GetPurchaseOrderRevisionsQueryHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-34")]
    public async Task Handle_ReturnsRevisionHistoryWithDiffs()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "Cost increase", "Please confirm",
            600m, po.ExpectedDeliveryDate.AddDays(5), po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        var revisions = await sender.Send(new GetPurchaseOrderRevisionsQuery(po.Id));

        // Send() itself creates Rev 0 (AC-63) - the amendment above is Rev 1.
        Assert.Equal(2, revisions.Count);
        var baseline = revisions.Single(r => r.RevisionNumber == 0);
        Assert.Equal(3, baseline.StatusId); // RevisionStatus.Superseded, by Rev 1 below

        var revision = revisions.Single(r => r.RevisionNumber == 1);
        Assert.Equal(600m, revision.UnitCost);
        Assert.Equal(po.UnitCost * po.Lines.Sum(l => l.Qty), revision.PoValueBefore); // before-figure reflects the prior unit cost
        Assert.Equal(600m * po.Lines.Sum(l => l.Qty), revision.PoValueAfter);
        Assert.Equal(revision.PoValueAfter - revision.PoValueBefore, revision.PoValueDiff);
        Assert.Equal(5, revision.ExpectedDateShiftDays);
        Assert.NotEmpty(revision.Lines);
    }
}
