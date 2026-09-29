using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.VendorResponses;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.VendorResponses;

public sealed class RecordVendorAmendmentRequestCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-30")]
    [Trait("Spec", "AC-31")]
    public async Task Handle_Acknowledged_CreatesVendorInitiatedPendingRevision()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var request = new CounterProposal(
            ReasonId: 1, ImpactNote: "Vendor asking for more lead time", VendorMessage: "Need 5 extra days",
            UnitCost: po.UnitCost, ExpectedDeliveryDate: po.ExpectedDeliveryDate.AddDays(5), LatestAcceptableDate: po.LatestAcceptableDate,
            OverTolerancePercent: null, UnderTolerancePercent: null, PaymentTermId: po.PaymentTermId, AdvancePercent: po.AdvancePercent,
            FabricResponsibilityId: po.FabricResponsibilityId, Lines: po.Lines);

        var revision = await sender.Send(new RecordVendorAmendmentRequestCommand(
            po.Id, ChannelId: 1, ResponderName: "Vendor Rep", ResponseDte: null, request));

        Assert.Equal((short)1, revision.RevisionNumber); // Rev 0 is the AC-63 baseline captured at Send
        Assert.Equal(1, revision.StatusId); // RevisionStatus.Pending
        Assert.Equal((short)2, revision.InitiatorId); // AmendmentInitiator.Vendor

        var reloadedPo = await sender.Send(new GetPurchaseOrderByIdQuery(po.Id));
        Assert.Equal(3, reloadedPo!.StatusId); // PoStatus.Acknowledged - unchanged, nothing overwritten
        Assert.Equal(po.ExpectedDeliveryDate, reloadedPo.ExpectedDeliveryDate); // still the baseline
    }

    [Fact]
    [Trait("Spec", "AC-30")]
    public async Task Handle_NotAcknowledged_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true)); // SentToVendor, not Acknowledged

        var request = new CounterProposal(
            ReasonId: 1, ImpactNote: "Vendor asking for more lead time", VendorMessage: null,
            UnitCost: po.UnitCost, ExpectedDeliveryDate: po.ExpectedDeliveryDate.AddDays(5), LatestAcceptableDate: po.LatestAcceptableDate,
            OverTolerancePercent: null, UnderTolerancePercent: null, PaymentTermId: po.PaymentTermId, AdvancePercent: po.AdvancePercent,
            FabricResponsibilityId: po.FabricResponsibilityId, Lines: po.Lines);

        var command = new RecordVendorAmendmentRequestCommand(po.Id, ChannelId: 1, ResponderName: "Vendor Rep", ResponseDte: null, request);

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Domain.DomainException>(() => sender.Send(command));
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public async Task Handle_MissingChannel_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var request = new CounterProposal(
            ReasonId: 1, ImpactNote: "Vendor asking for more lead time", VendorMessage: null,
            UnitCost: po.UnitCost, ExpectedDeliveryDate: po.ExpectedDeliveryDate.AddDays(5), LatestAcceptableDate: po.LatestAcceptableDate,
            OverTolerancePercent: null, UnderTolerancePercent: null, PaymentTermId: po.PaymentTermId, AdvancePercent: po.AdvancePercent,
            FabricResponsibilityId: po.FabricResponsibilityId, Lines: po.Lines);

        var command = new RecordVendorAmendmentRequestCommand(po.Id, ChannelId: 0, ResponderName: "Vendor Rep", ResponseDte: null, request);

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => sender.Send(command));
        Assert.Contains(nameof(RecordVendorAmendmentRequestCommand.ChannelId), exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task Handle_ExistingPendingRevision_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var firstRequest = new CounterProposal(
            ReasonId: 1, ImpactNote: "First ask", VendorMessage: null,
            UnitCost: po.UnitCost, ExpectedDeliveryDate: po.ExpectedDeliveryDate.AddDays(5), LatestAcceptableDate: po.LatestAcceptableDate,
            OverTolerancePercent: null, UnderTolerancePercent: null, PaymentTermId: po.PaymentTermId, AdvancePercent: po.AdvancePercent,
            FabricResponsibilityId: po.FabricResponsibilityId, Lines: po.Lines);
        await sender.Send(new RecordVendorAmendmentRequestCommand(po.Id, ChannelId: 1, ResponderName: "Vendor Rep", ResponseDte: null, firstRequest));

        var secondRequest = firstRequest with { ImpactNote = "Second ask", ExpectedDeliveryDate = po.ExpectedDeliveryDate.AddDays(10) };
        var command = new RecordVendorAmendmentRequestCommand(po.Id, ChannelId: 1, ResponderName: "Vendor Rep", ResponseDte: null, secondRequest);

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => sender.Send(command));
    }
}
