using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Catalog.Application.Styles;
using Romp.Modules.Catalog.Tests.Support;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Catalog.Tests.Styles;

public sealed class UpdateStyleCommandHandlerTests
{
    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Handle_ValidEdit_UpdatesFieldsAndAuditColumns()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var created = await sender.Send(new CreateStyleCommand(
            "STY-EDIT", "Basic Tee", null, 1, 1, 1, 1, 200m, 600m,
            ColourIds: [1], SizeIds: [1], TargetLines: [new StyleTargetLineDto(1, 1, 20)]));

        var updated = await sender.Send(new UpdateStyleCommand(
            created.Id, "Basic Tee V2", "Core", 2, 1, 1, 1, 250m, 650m,
            ColourIds: [1, 2], SizeIds: [1], TargetLines: [new StyleTargetLineDto(1, 2, 40)]));

        Assert.Equal("Basic Tee V2", updated.Name);
        Assert.Equal("Core", updated.CollectionName);
        Assert.Equal((short)2, updated.CategoryId);
        Assert.Equal(250m, updated.TargetUnitCost);
        Assert.Equal(2, updated.ColourIds.Count);
        Assert.Single(updated.TargetLines);
        Assert.Contains(updated.TargetLines, l => l.ColourId == 2 && l.TargetQty == 40);

        var reloaded = await sender.Send(new GetStyleByIdQuery(created.Id));
        Assert.NotNull(reloaded);
        Assert.Equal("Basic Tee V2", reloaded.Name);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    [Trait("Spec", "AC-3")]
    public async Task Handle_RemovingSizeOrColourInUseByActivePo_RejectedWithPoNumbers()
    {
        var poUsage = new FakePurchaseOrderUsageQueries();
        var services = TestServices.Build(Guid.NewGuid().ToString(), poUsage);
        var sender = services.GetRequiredService<ISender>();

        var created = await sender.Send(new CreateStyleCommand(
            "STY-GUARD", "Basic Tee", null, 1, 1, 1, 1, 200m, 600m,
            ColourIds: [1, 2], SizeIds: [1, 2], TargetLines: [new StyleTargetLineDto(1, 1, 20)]));

        // Size 2 / colour 2 are used by PO-2026-00001, a non-cancelled PO (fake stands in for VNDR - the guard never reads VNDR's tables directly, ADR 0002).
        poUsage.Usage.Add(new PoSizeColourUsage(SizeId: 2, ColourId: 2, PoNo: "PO-2026-00001"));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new UpdateStyleCommand(
            created.Id, "Basic Tee", null, 1, 1, 1, 1, 200m, 600m,
            ColourIds: [1, 2], SizeIds: [1], TargetLines: [new StyleTargetLineDto(1, 1, 20)])));

        Assert.Contains("PO-2026-00001", ex.Errors[nameof(UpdateStyleCommand.SizeIds)].Single());

        // Adding sizes/colours and editing other fields remains allowed (AC-2).
        var stillUnaffected = await sender.Send(new UpdateStyleCommand(
            created.Id, "Basic Tee V2", null, 1, 1, 1, 1, 200m, 600m,
            ColourIds: [1, 2, 3], SizeIds: [1, 2], TargetLines: [new StyleTargetLineDto(1, 1, 20)]));
        Assert.Equal("Basic Tee V2", stillUnaffected.Name);
    }
}
