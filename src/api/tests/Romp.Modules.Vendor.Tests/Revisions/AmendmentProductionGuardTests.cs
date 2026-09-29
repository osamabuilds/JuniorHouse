using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Vendor.Application.Files;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Revisions;
using Romp.Modules.Vendor.Application.VendorResponses;
using Romp.Modules.Vendor.Tests.PurchaseOrders;
using Romp.Modules.Vendor.Tests.Support;

namespace Romp.Modules.Vendor.Tests.Revisions;

/// <summary>Sprint 3 (AC-26, AC-27, D17): amendments and production - delivered quantities, the costly flag and the construction/commercial tiers.</summary>
public sealed class AmendmentProductionGuardTests
{
    private static CreateAmendmentCommand Amend(
        PoDto po, decimal? unitCost = null, IReadOnlyCollection<PoLineDto>? lines = null, short? fabric = -1,
        IReadOnlyCollection<AmendmentFileAdd>? addFiles = null, DateOnly? latestAcceptable = null) => new(
        po.Id, InitiatorId: 1, ReasonId: 1, ImpactNote: "note", VendorMessage: null,
        UnitCost: unitCost ?? po.UnitCost + 10m, po.ExpectedDeliveryDate, latestAcceptable ?? po.LatestAcceptableDate,
        po.OverTolerancePercent, po.UnderTolerancePercent, po.PaymentTermId, po.AdvancePercent,
        fabric == -1 ? po.FabricResponsibilityId : fabric, lines ?? po.Lines, addFiles);

    private static async Task<(IServiceProvider Provider, ISender Sender, PoDto Po)> AcknowledgedAsync()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        return (provider, sender, await PurchaseOrderLifecycleTests.AcknowledgedPoAsync(sender));
    }

    [Theory]
    [Trait("Spec", "AC-26")]
    [InlineData(ProductionProgress.Delivered)]
    public async Task Handle_DeliveredOrClosed_Refused(ProductionProgress progress)
    {
        var (_, sender, po) = await AcknowledgedAsync();
        await sender.Send(new ApplyProductionProgressCommand(po.Id, progress));

        var error = await Assert.ThrowsAsync<DomainException>(() => sender.Send(Amend(po)));
        Assert.Contains("Delivered or Closed", error.Message);

        await sender.Send(new ClosePurchaseOrderCommand(po.Id));
        await Assert.ThrowsAsync<DomainException>(() => sender.Send(Amend(po)));
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    public async Task Handle_LineBelowDelivered_Refused()
    {
        var (provider, sender, po) = await AcknowledgedAsync();
        provider.GetRequiredService<FakeProductionQueries>().Delivered.Add(new DeliveredQuantity(1, 10, 80)); // size 1 / colour 10 ordered 100

        var below = po.Lines.Select(l => l.SizeId == 1 && l.ColourId == 10 ? l with { Qty = 79 } : l).ToList();
        var error = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => sender.Send(Amend(po, lines: below)));
        Assert.Contains("already delivered", error.Message);

        // Exactly the delivered quantity is fine (never below it).
        var equal = po.Lines.Select(l => l.SizeId == 1 && l.ColourId == 10 ? l with { Qty = 80 } : l).ToList();
        var revision = await sender.Send(Amend(po, lines: equal));
        Assert.Equal((short)1, revision.RevisionNumber);
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    public async Task Handle_DroppingADeliveredLineEntirely_Refused()
    {
        var (provider, sender, po) = await AcknowledgedAsync();
        provider.GetRequiredService<FakeProductionQueries>().Delivered.Add(new DeliveredQuantity(2, 20, 10));

        var withoutLine = po.Lines.Where(l => !(l.SizeId == 2 && l.ColourId == 20)).ToList();

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() => sender.Send(Amend(po, lines: withoutLine)));
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public async Task Handle_AfterPpApproval_FlaggedCostly()
    {
        var (provider, sender, po) = await AcknowledgedAsync();
        provider.GetRequiredService<FakeProductionQueries>().PpSampleApproved = true;

        var revision = await sender.Send(Amend(po, unitCost: 700m));

        Assert.True(revision.IsCostlyAfterPpApproval);
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public async Task Handle_BeforeAnyPpApproval_NotFlaggedCostly()
    {
        var (_, sender, po) = await AcknowledgedAsync();

        var revision = await sender.Send(Amend(po, unitCost: 700m));

        Assert.False(revision.IsCostlyAfterPpApproval);
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public async Task Handle_CommercialAfterPpApproval_NewRoundOptional()
    {
        var (provider, sender, po) = await AcknowledgedAsync();
        provider.GetRequiredService<FakeProductionQueries>().PpSampleApproved = true;

        // Cost, date and terms only.
        var revision = await sender.Send(Amend(po, unitCost: 700m, latestAcceptable: po.LatestAcceptableDate!.Value.AddDays(7)));

        Assert.True(revision.IsCostlyAfterPpApproval);
        Assert.False(revision.IsConstructionChange); // only the costly note; a new PP round stays optional
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public async Task Handle_ConstructionAfterPpApproval_SuggestsNewRound_FabricResponsibilityChange()
    {
        var (provider, sender, po) = await AcknowledgedAsync();
        provider.GetRequiredService<FakeProductionQueries>().PpSampleApproved = true;

        var revision = await sender.Send(Amend(po, unitCost: po.UnitCost, fabric: 2)); // VendorSupplied -> RompSupplied

        Assert.True(revision.IsCostlyAfterPpApproval);
        Assert.True(revision.IsConstructionChange);
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public async Task Classify_FilesFabricColour_Construction()
    {
        // A file added.
        var (_, sender, po) = await AcknowledgedAsync();
        var withFile = await sender.Send(Amend(po, addFiles: [new AmendmentFileAdd(PoTestFiles.TechPack, "spec-v2.pdf", PoTestFiles.Pdf("v2"))]));
        Assert.True(withFile.IsConstructionChange);
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public async Task Classify_NewColour_Construction()
    {
        var provider = TestServices.Build(Guid.NewGuid().ToString());
        var sender = provider.GetRequiredService<ISender>();
        var vendor = await PoTestHelpers.CreateActiveVendorAsync(sender);
        var created = await sender.Send(new CreatePurchaseOrderCommand(
            vendor.Id, FakeStyleQueries.ActiveStyle.Id, 500m, new DateOnly(2026, 12, 1), Lines: [new PoLineDto(1, 10, 100)])); // colour 10 only
        var po = await sender.Send(new UpdatePurchaseOrderCommand(
            created.Id, created.UnitCost, created.ExpectedDeliveryDate, created.PaymentTermId, created.AdvancePercent, created.Lines,
            LatestAcceptableDate: created.ExpectedDeliveryDate.AddDays(14), FabricResponsibilityId: 1));
        await sender.Send(new SendPurchaseOrderCommand(po.Id, SendWithoutTechPack: true));
        po = await sender.Send(new AcknowledgePurchaseOrderCommand(po.Id));

        var revision = await sender.Send(Amend(po, unitCost: po.UnitCost, lines: [new PoLineDto(1, 10, 100), new PoLineDto(1, 20, 40)]));

        Assert.True(revision.IsConstructionChange); // colour 20 is new to this PO
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public async Task Classify_CostDateTerms_Commercial()
    {
        var (_, sender, po) = await AcknowledgedAsync();

        var revision = await sender.Send(Amend(po, unitCost: 900m, latestAcceptable: po.LatestAcceptableDate!.Value.AddDays(5)));

        Assert.False(revision.IsConstructionChange);
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public async Task Classify_QuantityChangeOnExistingColours_Commercial()
    {
        var (_, sender, po) = await AcknowledgedAsync();

        var revision = await sender.Send(Amend(po, unitCost: po.UnitCost, lines: po.Lines.Select(l => l with { Qty = l.Qty + 10 }).ToList()));

        Assert.False(revision.IsConstructionChange);
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public async Task Handle_VendorAmendmentRequest_AllowedWhileInProduction_AndGuardedByDeliveredQuantities()
    {
        var (provider, sender, po) = await AcknowledgedAsync();
        await sender.Send(new ApplyProductionProgressCommand(po.Id, ProductionProgress.Started));
        provider.GetRequiredService<FakeProductionQueries>().Delivered.Add(new DeliveredQuantity(1, 10, 60));

        CounterProposal Proposal(int size1Qty) => new(
            ReasonId: 1, ImpactNote: "vendor cost up", VendorMessage: null, UnitCost: po.UnitCost + 50m, po.ExpectedDeliveryDate, po.LatestAcceptableDate,
            po.OverTolerancePercent, po.UnderTolerancePercent, po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId,
            po.Lines.Select(l => l.SizeId == 1 && l.ColourId == 10 ? l with { Qty = size1Qty } : l).ToList());

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(new RecordVendorAmendmentRequestCommand(po.Id, 1, "Ali Raza", null, Proposal(50))));

        var ok = await sender.Send(new RecordVendorAmendmentRequestCommand(po.Id, 1, "Ali Raza", null, Proposal(90)));
        Assert.Equal(1, ok.StatusId); // Pending
    }
}
