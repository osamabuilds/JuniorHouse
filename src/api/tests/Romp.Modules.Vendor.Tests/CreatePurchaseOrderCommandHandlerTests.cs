using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Tests;

public sealed class CreatePurchaseOrderCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task Handle_ValidPo_CreatesInDraftWithGeneratedPoNo()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();

        var po = await PoTestHelpers.CreateDraftPoAsync(sender);

        Assert.True(po.Id > 0);
        Assert.Matches(@"^PO-\d{4}-\d{5}$", po.PoNo);
        Assert.Equal(1, po.StatusId); // PoStatus.Draft
        Assert.Equal(2, po.Lines.Count);
    }

    [Fact]
    [Trait("Spec", "AC-7a")]
    public async Task Handle_NoOverride_DefaultsPaymentTermAndAdvancePctFromVendor()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var vendor = await PoTestHelpers.CreateActiveVendorAsync(sender, FakePaymentTermQueries.TermId);

        var po = await PoTestHelpers.CreateDraftPoAsync(sender, vendor.Id);

        Assert.Equal(FakePaymentTermQueries.TermId, po.PaymentTermId);
        Assert.Equal(FakePaymentTermQueries.DefaultAdvancePercent, po.AdvancePercent);
    }

    [Fact]
    [Trait("Spec", "AC-7a")]
    public async Task Handle_WithOverride_UsesSubmittedPaymentTermAndAdvancePct()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var vendor = await PoTestHelpers.CreateActiveVendorAsync(sender);

        var po = await sender.Send(new CreatePurchaseOrderCommand(
            vendor.Id,
            FakeStyleQueries.ActiveStyle.Id,
            UnitCost: 500m,
            ExpectedDeliveryDate: new DateOnly(2026, 12, 1),
            Lines: [new PoLineDto(1, 10, 100)],
            PaymentTermId: 777,
            AdvancePercent: 15m));

        Assert.Equal((short)777, po.PaymentTermId);
        Assert.Equal(15m, po.AdvancePercent);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task Handle_LineSizeOrColourNotInStyle_RejectedWithValidationError()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var vendor = await PoTestHelpers.CreateActiveVendorAsync(sender);

        var command = new CreatePurchaseOrderCommand(
            vendor.Id,
            FakeStyleQueries.ActiveStyle.Id,
            UnitCost: 500m,
            ExpectedDeliveryDate: new DateOnly(2026, 12, 1),
            Lines: [new PoLineDto(SizeId: 99, ColourId: 10, Qty: 10)]);

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(command));

        Assert.Contains(nameof(CreatePurchaseOrderCommand.Lines), exception.Errors.Keys);
    }
}
