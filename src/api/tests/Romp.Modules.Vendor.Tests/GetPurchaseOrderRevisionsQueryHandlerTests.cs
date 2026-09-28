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
        await sender.Send(new SendPurchaseOrderCommand(po.Id));

        await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "Cost increase", "Please confirm",
            600m, po.ExpectedDeliveryDate.AddDays(5), po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        var revisions = await sender.Send(new GetPurchaseOrderRevisionsQuery(po.Id));

        var revision = Assert.Single(revisions);
        Assert.Equal((short)1, revision.RevisionNumber);
        Assert.Equal(600m, revision.UnitCost);
        Assert.Equal(po.UnitCost * po.Lines.Sum(l => l.Qty), revision.PoValueBefore); // before-figure reflects the prior unit cost
        Assert.Equal(600m * po.Lines.Sum(l => l.Qty), revision.PoValueAfter);
        Assert.Equal(revision.PoValueAfter - revision.PoValueBefore, revision.PoValueDiff);
        Assert.Equal(5, revision.ExpectedDateShiftDays);
        Assert.NotEmpty(revision.Lines);
    }
}
