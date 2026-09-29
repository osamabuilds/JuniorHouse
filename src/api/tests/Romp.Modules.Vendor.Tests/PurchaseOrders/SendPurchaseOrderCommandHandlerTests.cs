using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Application.Files;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.PurchaseOrders;

public sealed class SendPurchaseOrderCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task Handle_DraftPo_TransitionsAndWritesHistoryAndOutboxEvent()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        var sent = await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        Assert.Equal(2, sent.StatusId); // PoStatus.SentToVendor
        Assert.Equal(2, sent.StatusHistory.Count);
        Assert.Equal(1, sent.StatusHistory.First().PoStatusId);
        Assert.Equal(2, sent.StatusHistory.Last().PoStatusId);

        var dbContext = provider.GetRequiredService<VendorDbContext>();
        Assert.Contains(dbContext.Set<Romp.BuildingBlocks.Persistence.Outbox.OutboxMessage>(), m => m.EventType == "PoSentToVendorEvent");
    }

    /// <summary>
    /// SCRUM-93 task 18 (AC-6): unlike <see cref="PoTestHelpers.CreateDraftPoAsync"/> (which fills in
    /// valid terms so the rest of this sprint's Send-dependent tests keep passing), this builds a PO
    /// straight from <see cref="CreatePurchaseOrderCommand"/> so latest acceptable date and fabric
    /// responsibility stay unset, to exercise the rejection itself.
    /// </summary>
    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task Handle_MissingLatestAcceptableDateOrFabricResponsibility_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var vendor = await PoTestHelpers.CreateActiveVendorAsync(sender);

        var po = await sender.Send(new CreatePurchaseOrderCommand(
            vendor.Id,
            FakeStyleQueries.ActiveStyle.Id,
            UnitCost: 500m,
            ExpectedDeliveryDate: new DateOnly(2026, 12, 1),
            Lines: [new PoLineDto(1, 10, 100)]));

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true)));

        Assert.Contains("LatestAcceptableDate", exception.Errors.Keys);
        Assert.Contains("FabricResponsibilityId", exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-39")]
    public async Task Handle_NoTechPackSpecFile_RequiresExplicitConfirmAndRecordsIt()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        var rejected = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(new SendPurchaseOrderCommand(po.Id)));
        Assert.Contains("no Tech Pack Spec attached", rejected.Message);

        var sent = await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        Assert.Contains(sent.StatusHistory, h => h.PoStatusId == 2 && h.Note!.Contains("without a Tech Pack Spec"));
    }

    [Fact]
    [Trait("Spec", "AC-39")]
    public async Task Handle_TechPackSpecAttached_SendsWithoutConfirmation()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new UploadPoFileCommand(po.Id, PoTestFiles.TechPack, "spec.pdf", PoTestFiles.Pdf()));

        var sent = await sender.Send(new SendPurchaseOrderCommand(po.Id));

        Assert.Equal(2, sent.StatusId);
        Assert.All(sent.StatusHistory, h => Assert.Null(h.Note));
    }
}
