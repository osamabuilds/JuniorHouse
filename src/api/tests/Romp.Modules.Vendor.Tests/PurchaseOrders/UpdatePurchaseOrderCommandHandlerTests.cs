using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.PurchaseOrders;

public sealed class UpdatePurchaseOrderCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task Handle_DraftPo_SavesChanges()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        var updated = await sender.Send(new UpdatePurchaseOrderCommand(
            po.Id, UnitCost: 600m, ExpectedDeliveryDate: new DateOnly(2027, 1, 15),
            PaymentTermId: 5, AdvancePercent: 20m, Lines: [new PoLineDto(1, 10, 200)]));

        Assert.Equal(600m, updated.UnitCost);
        Assert.Equal(new DateOnly(2027, 1, 15), updated.ExpectedDeliveryDate);
        Assert.Single(updated.Lines);
        Assert.Equal(200, updated.Lines.Single().Qty);
    }

    /// <summary>SCRUM-93 task 17 (AC-5, AC-8): the new commercial terms save in place on the same Draft PO, consuming no revision (there is no revision concept yet in this sprint's slice - ADR 0007's work lands later).</summary>
    [Fact]
    [Trait("Spec", "AC-5")]
    [Trait("Spec", "AC-8")]
    public async Task Handle_NewCommercialTerms_SavesInPlaceNoRevision()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        var updated = await sender.Send(new UpdatePurchaseOrderCommand(
            po.Id, po.UnitCost, po.ExpectedDeliveryDate, po.PaymentTermId, po.AdvancePercent, po.Lines,
            LatestAcceptableDate: po.ExpectedDeliveryDate.AddDays(21),
            OverTolerancePercent: 5m,
            UnderTolerancePercent: 5m,
            FabricResponsibilityId: 2));

        Assert.Equal(po.ExpectedDeliveryDate.AddDays(21), updated.LatestAcceptableDate);
        Assert.Equal(5m, updated.OverTolerancePercent);
        Assert.Equal(5m, updated.UnderTolerancePercent);
        Assert.Equal((short)2, updated.FabricResponsibilityId);
        Assert.Equal(1, updated.StatusId); // still Draft - AC-8: saved in place, no status/revision change.
        Assert.Single(updated.StatusHistory); // only the original "created into Draft" entry.
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task Handle_LatestAcceptableDateBeforeExpectedDate_RejectedWithValidationError()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        var command = new UpdatePurchaseOrderCommand(
            po.Id, po.UnitCost, po.ExpectedDeliveryDate, po.PaymentTermId, po.AdvancePercent, po.Lines,
            LatestAcceptableDate: po.ExpectedDeliveryDate.AddDays(-1));

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(command));

        Assert.Contains(nameof(UpdatePurchaseOrderCommand.LatestAcceptableDate), exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task Handle_ToleranceAboveConfiguredMaximum_RejectedWithValidationError()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        var command = new UpdatePurchaseOrderCommand(
            po.Id, po.UnitCost, po.ExpectedDeliveryDate, po.PaymentTermId, po.AdvancePercent, po.Lines,
            OverTolerancePercent: 21m); // default max is 20% (PoCommercialTermsOptions)

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(command));

        Assert.Contains(nameof(UpdatePurchaseOrderCommand.OverTolerancePercent), exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task Handle_AcknowledgedPo_RejectedWithDomainError()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var po = await PoTestHelpers.CreateDraftPoAsync(sender);
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Domain.DomainException>(() => sender.Send(
            new UpdatePurchaseOrderCommand(po.Id, 700m, new DateOnly(2027, 2, 1), 1, 10m, [new PoLineDto(1, 10, 10)])));
    }
}
