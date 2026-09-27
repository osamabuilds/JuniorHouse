using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Domain;

namespace Romp.Modules.Catalog.Application;

/// <summary>AC-3: creates a style with its colourways, size run and target-quantity grid.</summary>
public sealed record CreateStyleCommand(
    string Code,
    string Name,
    string? CollectionName,
    short CategoryId,
    short GenderId,
    short AgeBracketId,
    short FabricId,
    decimal TargetUnitCost,
    decimal TargetRetailPrice,
    IReadOnlyCollection<short> ColourIds,
    IReadOnlyCollection<short> SizeIds,
    IReadOnlyCollection<StyleTargetLineDto> TargetLines) : IRequest<StyleDto>, ICatalogCommand;

public sealed class CreateStyleCommandValidator : AbstractValidator<CreateStyleCommand>
{
    public CreateStyleCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(30);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.CollectionName).MaximumLength(100);
        RuleFor(c => c.TargetUnitCost).GreaterThanOrEqualTo(0);
        RuleFor(c => c.TargetRetailPrice).GreaterThanOrEqualTo(0);

        // AC-3: at least one colourway and one size.
        RuleFor(c => c.ColourIds).NotEmpty().WithMessage("At least one colourway is required.");
        RuleFor(c => c.SizeIds).NotEmpty().WithMessage("At least one size is required.");

        // The style's own colourways/size run are the only source of truth for what's a valid
        // target-line cell (plan.md's STYL_TGT_LINE invariant).
        RuleFor(c => c.TargetLines)
            .Must((command, targetLines) => targetLines.All(line =>
                command.SizeIds.Contains(line.SizeId) && command.ColourIds.Contains(line.ColourId)))
            .WithMessage("Every target line's size and colour must be part of this style's size run and colourways.");
    }
}

public sealed class CreateStyleCommandHandler(ICatalogDbContext dbContext) : IRequestHandler<CreateStyleCommand, StyleDto>
{
    public async Task<StyleDto> Handle(CreateStyleCommand request, CancellationToken cancellationToken)
    {
        var alreadyExists = await dbContext.Styles.AnyAsync(s => s.Code == request.Code, cancellationToken);

        if (alreadyExists)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(
                new Dictionary<string, string[]> { [nameof(request.Code)] = [$"Code '{request.Code}' already exists."] });
        }

        var style = new Style(
            request.Code,
            request.Name,
            request.CollectionName,
            request.CategoryId,
            request.GenderId,
            request.AgeBracketId,
            request.FabricId,
            request.TargetUnitCost,
            request.TargetRetailPrice);

        dbContext.Styles.Add(style);

        // Saved once here so Style.Id is generated before SetColourSizeAndTargets fixes up the
        // children's StyleId, and again so the response DTO carries real ids throughout.
        await dbContext.SaveChangesAsync(cancellationToken);

        style.SetColourSizeAndTargets(
            request.ColourIds,
            request.SizeIds,
            request.TargetLines.Select(line => (line.SizeId, line.ColourId, line.TargetQty)));

        await dbContext.SaveChangesAsync(cancellationToken);

        return style.ToDto();
    }
}
