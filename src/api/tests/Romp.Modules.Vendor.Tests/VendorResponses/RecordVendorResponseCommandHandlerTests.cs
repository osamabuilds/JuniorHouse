using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Vendor.Application.Files;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Revisions;
using Romp.Modules.Vendor.Application.VendorResponses;
using Romp.Modules.Vendor.Application.VendorView;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.VendorResponses;

public sealed class RecordVendorResponseCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-25")]
    public async Task Handle_ConfirmedAsSent_TransitionsToAcknowledged()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        var result = await sender.Send(new RecordVendorResponseCommand(
            po.Id, OutcomeTypeId: 1, RevisionNumber: 0, ChannelId: 1, ResponderName: "Vendor Rep",
            ResponseDte: null, Counter: null));

        Assert.Equal(3, result.Po.StatusId); // PoStatus.Acknowledged
        Assert.Null(result.SuggestedCancelReasonId);
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    public async Task Handle_StaleRevisionNumber_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        var command = new RecordVendorResponseCommand(
            po.Id, OutcomeTypeId: 1, RevisionNumber: 5 /* not the real current revision (0) */, ChannelId: 1,
            ResponderName: "Vendor Rep", ResponseDte: null, Counter: null);

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => sender.Send(command));
        Assert.Contains(nameof(RecordVendorResponseCommand.RevisionNumber), exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public async Task Handle_Countered_CreatesVendorInitiatedPendingRevision()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        var counter = new CounterProposal(
            ReasonId: 1, ImpactNote: "Vendor wants more", VendorMessage: null,
            UnitCost: 650m, ExpectedDeliveryDate: po.ExpectedDeliveryDate, LatestAcceptableDate: po.LatestAcceptableDate,
            OverTolerancePercent: null, UnderTolerancePercent: null, PaymentTermId: po.PaymentTermId, AdvancePercent: po.AdvancePercent,
            FabricResponsibilityId: po.FabricResponsibilityId, Lines: po.Lines);

        var result = await sender.Send(new RecordVendorResponseCommand(
            po.Id, OutcomeTypeId: 2, RevisionNumber: 0, ChannelId: 1, ResponderName: "Vendor Rep",
            ResponseDte: null, Counter: counter));

        Assert.NotNull(result.Revision);
        Assert.Equal(1, result.Revision!.StatusId); // RevisionStatus.Pending
        Assert.Equal((short)2, result.Revision.InitiatorId); // AmendmentInitiator.Vendor
        Assert.Equal(2, result.Po.StatusId); // PoStatus.SentToVendor - unchanged, nothing overwritten (AC-27)
        Assert.Equal(po.UnitCost, result.Po.UnitCost); // still the baseline, not the vendor's counter
    }

    [Fact]
    [Trait("Spec", "AC-29")]
    public async Task Handle_Declined_ReturnsCancelSignal()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        var result = await sender.Send(new RecordVendorResponseCommand(
            po.Id, OutcomeTypeId: 3, RevisionNumber: 0, ChannelId: 1, ResponderName: "Vendor Rep",
            ResponseDte: null, Counter: null));

        Assert.Equal(2, result.Po.StatusId); // PoStatus.SentToVendor - no state change of its own
        Assert.Equal((short)1, result.SuggestedCancelReasonId); // PO_CNCL_RSN_LKP.VendorDeclined
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public async Task Handle_MissingChannel_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        var command = new RecordVendorResponseCommand(
            po.Id, OutcomeTypeId: 1, RevisionNumber: 0, ChannelId: 0, ResponderName: "Vendor Rep",
            ResponseDte: null, Counter: null);

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => sender.Send(command));
        Assert.Contains(nameof(RecordVendorResponseCommand.ChannelId), exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    [Trait("Spec", "AC-34")]
    public async Task Handle_WithEvidence_StoredAsInternalFileAndShownInHistory()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        await sender.Send(new RecordVendorResponseCommand(
            po.Id, 1, 0, 1, "Ali Raza", null, null, [new EvidenceFileAdd("whatsapp.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1])]));

        var revisions = await sender.Send(new GetPurchaseOrderRevisionsQuery(po.Id));
        var evidence = Assert.Single(Assert.Single(revisions.Single(r => r.RevisionNumber == 0).Communications!).Evidence!);
        Assert.Equal("whatsapp.png", evidence.FileName);

        var files = await sender.Send(new GetPoFileListQuery(po.Id));
        Assert.False(Assert.Single(files).IsVendorVisible); // internal: never shown to the vendor
        var vendorView = await sender.Send(new GetVendorFacingPoViewQuery(po.Id));
        Assert.Empty(vendorView!.Files);
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public async Task Handle_EvidenceOfDisallowedType_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => sender.Send(new RecordVendorResponseCommand(
            po.Id, 1, 0, 1, "Ali Raza", null, null, [new EvidenceFileAdd("x.exe", "MZ"u8.ToArray())])));

        Assert.Contains("evidence file has a type that isn't allowed", exception.Message);
    }
}
