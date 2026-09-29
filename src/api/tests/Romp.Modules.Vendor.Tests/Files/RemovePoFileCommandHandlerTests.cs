using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Vendor.Application.Files;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.Files;

public sealed class RemovePoFileCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-37")]
    public async Task Handle_DraftFile_SoftRemovedAndNoLongerListed()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        var file = await sender.Send(new UploadPoFileCommand(po.Id, 1, "spec.pdf", PoTestFiles.Pdf()));

        await sender.Send(new RemovePoFileCommand(po.Id, file.Id));

        Assert.Empty(await sender.Send(new GetPoFileListQuery(po.Id)));
    }

    [Fact]
    [Trait("Spec", "AC-41")]
    public async Task Handle_PostSendInternalFile_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        var file = await sender.Send(new UploadPoFileCommand(po.Id, 6, "cost.pdf", PoTestFiles.Pdf()));
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        var exception = await Assert.ThrowsAsync<DomainException>(() => sender.Send(new RemovePoFileCommand(po.Id, file.Id)));

        Assert.Contains("internal files can no longer be removed", exception.Message);
    }

    [Fact]
    [Trait("Spec", "AC-40")]
    public async Task Handle_PostSendVendorVisibleFile_RejectedPointsToAmend()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        var file = await sender.Send(new UploadPoFileCommand(po.Id, 1, "spec.pdf", PoTestFiles.Pdf()));
        await sender.Send(new SendPurchaseOrderCommand(po.Id));

        var exception = await Assert.ThrowsAsync<DomainException>(() => sender.Send(new RemovePoFileCommand(po.Id, file.Id)));

        Assert.Contains("amendment", exception.Message);
    }

    [Fact]
    [Trait("Spec", "AC-42")]
    public async Task Handle_CancelledPo_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        var file = await sender.Send(new UploadPoFileCommand(po.Id, 1, "spec.pdf", PoTestFiles.Pdf()));
        await sender.Send(new CancelPurchaseOrderCommand(po.Id, 6));

        var exception = await Assert.ThrowsAsync<DomainException>(() => sender.Send(new RemovePoFileCommand(po.Id, file.Id)));

        Assert.Contains("cancelled", exception.Message);
        Assert.Single(await sender.Send(new GetPoFileListQuery(po.Id))); // still listed and downloadable
    }
}
