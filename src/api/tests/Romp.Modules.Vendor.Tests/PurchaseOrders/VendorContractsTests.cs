using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Application.Files;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Revisions;
using Romp.Modules.Vendor.Contracts;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.PurchaseOrders;

public sealed class VendorContractsTests
{
    [Fact]
    [Trait("Spec", "AC-51")]
    public async Task Queries_ReturnExpectedShapesForFutureConsumers()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var queries = provider.GetRequiredService<IPurchaseOrderQueries>();

        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        Assert.Null(await queries.GetInForceTermsAsync(po.Id, CancellationToken.None)); // a Draft isn't visible

        var original = await sender.Send(new UploadPoFileCommand(po.Id, PoTestFiles.TechPack, "spec-v1.pdf", PoTestFiles.Pdf("v1")));
        await sender.Send(new SendPurchaseOrderCommand(po.Id));
        await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 8, "note", null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines,
            [new AmendmentFileAdd(PoTestFiles.TechPack, "spec-v2.pdf", PoTestFiles.Pdf("v2"))], [original.Id]));

        var terms = await queries.GetInForceTermsAsync(po.Id, CancellationToken.None);
        Assert.NotNull(terms);
        Assert.Equal((short)1, terms.RevisionNumber);
        Assert.Equal(600m, terms.UnitCost);
        Assert.Equal(2, terms.Lines.Count);

        var revisions = await queries.ListRevisionsAsync(po.Id, CancellationToken.None);
        Assert.Equal([(short)0, (short)1], revisions.Select(r => r.RevisionNumber));
        Assert.Equal([3, 2], revisions.Select(r => (int)r.StatusId)); // Rev 0 Superseded, Rev 1 In force

        Assert.Equal(["spec-v2.pdf"], (await queries.GetEffectiveFilesAsync(po.Id, null, CancellationToken.None)).Select(f => f.FileName));
        Assert.Equal(["spec-v1.pdf"], (await queries.GetEffectiveFilesAsync(po.Id, 0, CancellationToken.None)).Select(f => f.FileName));
    }
}
