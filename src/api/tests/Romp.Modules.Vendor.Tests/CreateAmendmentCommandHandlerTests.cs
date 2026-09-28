using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Infrastructure;

namespace Romp.Modules.Vendor.Tests;

public sealed class CreateAmendmentCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task Handle_SentToVendor_NewRevisionImmediatelyInForce()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id));

        var revision = await sender.Send(new CreateAmendmentCommand(
            po.Id, InitiatorId: 1, ReasonId: 1, ImpactNote: "Vendor cost increase", VendorMessage: null,
            UnitCost: 550m, ExpectedDeliveryDate: po.ExpectedDeliveryDate, LatestAcceptableDate: po.LatestAcceptableDate,
            OverTolerancePercent: null, UnderTolerancePercent: null, PaymentTermId: po.PaymentTermId, AdvancePercent: po.AdvancePercent,
            FabricResponsibilityId: po.FabricResponsibilityId, Lines: po.Lines));

        Assert.Equal((short)1, revision.RevisionNumber);
        Assert.Equal(2, revision.StatusId); // RevisionStatus.InForce

        var reloadedPo = await sender.Send(new GetPurchaseOrderByIdQuery(po.Id));
        Assert.Equal(550m, reloadedPo!.UnitCost);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task Handle_Acknowledged_NewRevisionPending()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var revision = await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "Vendor cost increase", null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        Assert.Equal((short)1, revision.RevisionNumber);
        Assert.Equal(1, revision.StatusId); // RevisionStatus.Pending

        var reloadedPo = await sender.Send(new GetPurchaseOrderByIdQuery(po.Id));
        Assert.Equal(500m, reloadedPo!.UnitCost); // unchanged - the Pending revision hasn't taken effect
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public async Task Handle_NoOpChange_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id));

        var command = new CreateAmendmentCommand(
            po.Id, 1, 1, "No real change", null,
            po.UnitCost, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            po.OverTolerancePercent, po.UnderTolerancePercent, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines);

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => sender.Send(command));
        Assert.Contains(nameof(CreateAmendmentCommand.PoId), exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-17")]
    public async Task Handle_MissingReasonOrImpactNote_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id));

        var command = new CreateAmendmentCommand(
            po.Id, 1, ReasonId: 0, ImpactNote: "", VendorMessage: null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines);

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => sender.Send(command));
        Assert.Contains(nameof(CreateAmendmentCommand.ReasonId), exception.Errors.Keys);
        Assert.Contains(nameof(CreateAmendmentCommand.ImpactNote), exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-20")]
    public async Task Handle_DraftOrCancelled_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        var command = new CreateAmendmentCommand(
            po.Id, 1, 1, "note", null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines);

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Domain.DomainException>(() => sender.Send(command));
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public async Task Handle_ExistingPendingRevision_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "First amendment", null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        var secondCommand = new CreateAmendmentCommand(
            po.Id, 1, 1, "Second amendment", null,
            700m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines);

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => sender.Send(secondCommand));
        Assert.Contains(nameof(CreateAmendmentCommand.PoId), exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public async Task Handle_AmendableFieldSet_AppliesOnlyAllowedFields()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id));

        var revision = await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "Full amendment", "Please confirm",
            UnitCost: 999m,
            ExpectedDeliveryDate: po.ExpectedDeliveryDate.AddDays(10),
            LatestAcceptableDate: po.ExpectedDeliveryDate.AddDays(20),
            OverTolerancePercent: 5m,
            UnderTolerancePercent: 5m,
            PaymentTermId: 2,
            AdvancePercent: 30m,
            FabricResponsibilityId: 2,
            Lines: [new PoLineDto(1, 20, 999)]));

        Assert.Equal(999m, revision.UnitCost);
        Assert.Equal(po.ExpectedDeliveryDate.AddDays(10), revision.ExpectedDeliveryDate);
        Assert.Equal(po.ExpectedDeliveryDate.AddDays(20), revision.LatestAcceptableDate);
        Assert.Equal(5m, revision.OverTolerancePercent);
        Assert.Equal(5m, revision.UnderTolerancePercent);
        Assert.Equal((short)2, revision.PaymentTermId);
        Assert.Equal(30m, revision.AdvancePercent);
        Assert.Equal((short)2, revision.FabricResponsibilityId);
        Assert.Single(revision.Lines);
        Assert.Equal(999, revision.Lines.Single().Qty);

        var reloadedPo = await sender.Send(new GetPurchaseOrderByIdQuery(po.Id));
        Assert.Equal(999m, reloadedPo!.UnitCost);
        Assert.Equal(po.VendorId, reloadedPo.VendorId); // not on the command at all - not amendable
        Assert.Equal(po.StyleId, reloadedPo.StyleId); // not on the command at all - not amendable
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public async Task Handle_VendorOrStyleChange_Rejected()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id));

        // CreateAmendmentCommand has no vendor/style field at all - the only way an amendment could
        // implicitly reach outside the PO's own style is via a line referencing a size or colour the
        // style doesn't have, which this rejects (Sprint 1's AC-8 pattern, reused here per AC-15).
        var command = new CreateAmendmentCommand(
            po.Id, 1, 1, "Invalid line", null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId,
            Lines: [new PoLineDto(SizeId: 99, ColourId: 10, Qty: 10)]);

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => sender.Send(command));
        Assert.Contains(nameof(CreateAmendmentCommand.Lines), exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-47")]
    public async Task Handle_AnyRevision_WritesOutboxEvent()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id));

        await sender.Send(new CreateAmendmentCommand(
            po.Id, 1, 1, "note", null,
            600m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            null, null, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId, po.Lines));

        var dbContext = provider.GetRequiredService<VendorDbContext>();
        Assert.Contains(dbContext.Set<Romp.BuildingBlocks.Persistence.OutboxMessage>(), m => m.EventType == "PoRevisionPutInForceEvent");
    }
}
