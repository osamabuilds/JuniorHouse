using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Domain;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Catalog.Application;

/// <summary>AC-3: edits a style's fields, colourways, size run and target-quantity grid.</summary>
public sealed record UpdateStyleCommand(
    long Id,
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

public sealed class UpdateStyleCommandValidator : AbstractValidator<UpdateStyleCommand>
{
    public UpdateStyleCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.CollectionName).MaximumLength(100);
        RuleFor(c => c.TargetUnitCost).GreaterThanOrEqualTo(0);
        RuleFor(c => c.TargetRetailPrice).GreaterThanOrEqualTo(0);
        RuleFor(c => c.ColourIds).NotEmpty().WithMessage("At least one colourway is required.");
        RuleFor(c => c.SizeIds).NotEmpty().WithMessage("At least one size is required.");
        RuleFor(c => c.TargetLines)
            .Must((command, targetLines) => targetLines.All(line =>
                command.SizeIds.Contains(line.SizeId) && command.ColourIds.Contains(line.ColourId)))
            .WithMessage("Every target line's size and colour must be part of this style's size run and colourways.");
    }
}

public sealed class UpdateStyleCommandHandler(ICatalogDbContext dbContext, IPurchaseOrderUsageQueries poUsageQueries)
    : IRequestHandler<UpdateStyleCommand, StyleDto>
{
    public async Task<StyleDto> Handle(UpdateStyleCommand request, CancellationToken cancellationToken)
    {
        var style = await dbContext.Styles
            .Include(s => s.Colourways)
            .Include(s => s.Sizes)
            .Include(s => s.TargetLines)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Style {request.Id} was not found.");

        // AC-2, AC-3: a size/colour still used by a non-cancelled PO's line can't be removed -
        // only VNDR's contract query is consulted, never VNDR's own tables (ADR 0002).
        var removedSizeIds = style.Sizes.Select(s => s.SizeId).Except(request.SizeIds).ToHashSet();
        var removedColourIds = style.Colourways.Select(c => c.ColourId).Except(request.ColourIds).ToHashSet();

        if (removedSizeIds.Count > 0 || removedColourIds.Count > 0)
        {
            var usage = await poUsageQueries.GetActiveSizeColourUsageAsync(request.Id, cancellationToken);
            var blockingPoNos = usage
                .Where(u => removedSizeIds.Contains(u.SizeId) || removedColourIds.Contains(u.ColourId))
                .Select(u => u.PoNo)
                .Distinct()
                .Order()
                .ToList();

            if (blockingPoNos.Count > 0)
            {
                throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
                {
                    [nameof(request.SizeIds)] =
                        [$"Size/colour still used by PO(s) {string.Join(", ", blockingPoNos)} cannot be removed."],
                });
            }
        }

        style.UpdateDetails(
            request.Name,
            request.CollectionName,
            request.CategoryId,
            request.GenderId,
            request.AgeBracketId,
            request.FabricId,
            request.TargetUnitCost,
            request.TargetRetailPrice);

        style.SetColourSizeAndTargets(
            request.ColourIds,
            request.SizeIds,
            request.TargetLines.Select(line => (line.SizeId, line.ColourId, line.TargetQty)));

        // Saved here (not left to CatalogTransactionBehavior) so the response DTO reflects
        // UPDT_DTE/BY and the new target lines' INSR_DTE/BY as stamped by
        // AuditSaveChangesInterceptor, not the pre-save in-memory values.
        await dbContext.SaveChangesAsync(cancellationToken);

        return style.ToDto();
    }
}
