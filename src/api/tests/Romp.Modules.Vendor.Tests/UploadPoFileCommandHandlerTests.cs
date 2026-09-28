using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Tests;

public sealed class UploadPoFileCommandHandlerTests
{
    private const short TechPack = PoTestFiles.TechPack;
    private const short CostSheet = PoTestFiles.CostSheet;

    private static async Task<(IServiceProvider Provider, ISender Sender, PoDto Po)> CreateAsync()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        return (provider, sender, await PoTestHelpers.CreateDraftPoAsync(sender));
    }

    [Fact]
    [Trait("Spec", "AC-37")]
    public async Task Handle_DraftAnyCategory_StoredAndListed()
    {
        var (_, sender, po) = await CreateAsync();

        await sender.Send(new UploadPoFileCommand(po.Id, TechPack, "spec.pdf", PoTestFiles.Pdf()));
        await sender.Send(new UploadPoFileCommand(po.Id, CostSheet, "cost.pdf", PoTestFiles.Pdf()));

        var files = await sender.Send(new GetPoFileListQuery(po.Id));
        Assert.Equal(2, files.Count);
        Assert.Contains(files, f => f.FileName == "spec.pdf" && f.IsVendorVisible);
        Assert.Contains(files, f => f.FileName == "cost.pdf" && !f.IsVendorVisible);
    }

    [Fact]
    [Trait("Spec", "AC-40")]
    public async Task Handle_PostSendVendorVisible_RejectedPointsToAmend()
    {
        var (_, sender, po) = await CreateAsync();
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => sender.Send(new UploadPoFileCommand(po.Id, TechPack, "spec.pdf", PoTestFiles.Pdf())));

        Assert.Contains("amendment", exception.Message);
    }

    [Fact]
    [Trait("Spec", "AC-41")]
    public async Task Handle_PostSendInternal_Allowed()
    {
        var (_, sender, po) = await CreateAsync();
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        var file = await sender.Send(new UploadPoFileCommand(po.Id, CostSheet, "cost.pdf", PoTestFiles.Pdf()));

        Assert.False(file.IsVendorVisible);
    }

    [Fact]
    [Trait("Spec", "AC-45")]
    public async Task Handle_InvalidTypeOrOverLimit_RejectedNothingStored()
    {
        var (provider, sender, po) = await CreateAsync();
        var storage = (InMemoryFileStorage)provider.GetRequiredService<IFileStorage>();

        var badType = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(new UploadPoFileCommand(po.Id, TechPack, "evil.pdf", "MZ-not-a-pdf"u8.ToArray())));
        Assert.Contains("isn't allowed", badType.Message);

        var tooBig = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(new UploadPoFileCommand(po.Id, TechPack, "big.pdf", PoTestFiles.Pdf(new string('x', 2000)))));
        Assert.Contains("larger than", tooBig.Message);

        for (var i = 0; i < 3; i++)
        {
            await sender.Send(new UploadPoFileCommand(po.Id, TechPack, $"f{i}.pdf", PoTestFiles.Pdf()));
        }

        var overCount = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(new UploadPoFileCommand(po.Id, TechPack, "f4.pdf", PoTestFiles.Pdf())));
        Assert.Contains("maximum of 3 files", overCount.Message);

        Assert.Equal(3, storage.Count); // only the three accepted uploads
    }

    [Fact]
    [Trait("Spec", "AC-42")]
    public async Task Handle_CancelledPo_Rejected()
    {
        var (_, sender, po) = await CreateAsync();
        await sender.Send(new CancelPurchaseOrderCommand(po.Id, 6));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => sender.Send(new UploadPoFileCommand(po.Id, CostSheet, "cost.pdf", PoTestFiles.Pdf())));

        Assert.Contains("cancelled", exception.Message);
    }
}
