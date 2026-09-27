using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Catalog.Application;

namespace Romp.Modules.Catalog.Tests;

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
}
