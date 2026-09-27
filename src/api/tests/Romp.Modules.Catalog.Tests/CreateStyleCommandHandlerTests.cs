using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Catalog.Application;

namespace Romp.Modules.Catalog.Tests;

public sealed class CreateStyleCommandHandlerTests
{
    private static CreateStyleCommand ValidCommand(string code = "STY-001") => new(
        code,
        "Floral Summer Dress",
        "Summer 2026",
        CategoryId: 3,
        GenderId: 2,
        AgeBracketId: 3,
        FabricId: 1,
        TargetUnitCost: 450m,
        TargetRetailPrice: 1200m,
        ColourIds: [1, 2],
        SizeIds: [4, 5],
        TargetLines: [new StyleTargetLineDto(4, 1, 50), new StyleTargetLineDto(5, 2, 30)]);

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Handle_ValidStyle_PersistsWithMapsAndTargetLines()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();

        var result = await sender.Send(ValidCommand());

        Assert.True(result.Id > 0);
        Assert.Equal(2, result.ColourIds.Count);
        Assert.Equal(2, result.SizeIds.Count);
        Assert.Equal(2, result.TargetLines.Count);
        Assert.Contains(result.TargetLines, l => l.SizeId == 4 && l.ColourId == 1 && l.TargetQty == 50);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task Handle_DuplicateCode_RejectedWithValidationError()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        await sender.Send(ValidCommand("STY-DUP"));

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(ValidCommand("STY-DUP")));

        Assert.Contains("Code", exception.Errors.Keys);
    }

    [Fact]
    [Trait("Spec", "SCRUM-173")]
    public async Task Handle_TargetLineForSizeOrColourNotInStyle_RejectedWithValidationError()
    {
        var sender = TestServices.Build(Guid.NewGuid().ToString()).GetRequiredService<ISender>();
        var invalid = ValidCommand("STY-BAD") with
        {
            TargetLines = [new StyleTargetLineDto(SizeId: 99, ColourId: 1, TargetQty: 10)],
        };

        var exception = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(
            () => sender.Send(invalid));

        Assert.Contains(nameof(CreateStyleCommand.TargetLines), exception.Errors.Keys);
    }
}
