using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Domain.Bins;

namespace Romp.Modules.Warehouse.Application.Bins;

/// <summary>Adds a bin from zone, aisle and shelf (BIN-1). A second bin with the same code is refused (BIN-2).</summary>
public sealed record CreateBinCommand(string Zone, string Aisle, string Shelf, string ActingPerson)
    : IRequest<BinRowDto>, IWarehouseCommand;

public sealed class CreateBinCommandValidator : AbstractValidator<CreateBinCommand>
{
    public CreateBinCommandValidator()
    {
        // The pattern lives in Bin.Check (one source of truth); the validator turns a bad part into a 400 with a field error, and the
        // entity still refuses it if anything ever skips validation.
        RuleFor(c => c.Zone).NotEmpty().MaximumLength(10)
            .Must((c, _) => Bin.Check(c.Zone, "1", "A") is null).WithMessage("The zone must be 1 to 3 capital letters or digits.");
        RuleFor(c => c.Aisle).NotEmpty().MaximumLength(10)
            .Must((c, _) => Bin.Check("A", c.Aisle, "A") is null).WithMessage("The aisle must be 1 to 3 digits.");
        RuleFor(c => c.Shelf).NotEmpty().MaximumLength(10)
            .Must((c, _) => Bin.Check("A", "1", c.Shelf) is null).WithMessage("The shelf must be 1 or 2 capital letters or digits.");
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class CreateBinCommandHandler(IWarehouseDbContext dbContext) : IRequestHandler<CreateBinCommand, BinRowDto>
{
    public async Task<BinRowDto> Handle(CreateBinCommand request, CancellationToken cancellationToken)
    {
        var bin = new Bin(request.Zone, request.Aisle, request.Shelf);

        if (await dbContext.Bins.AnyAsync(b => b.BinCode == bin.BinCode, cancellationToken))
        {
            throw new DomainException($"Bin {bin.BinCode} already exists.");
        }

        dbContext.Bins.Add(bin);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new BinRowDto(bin.Id, bin.BinCode, bin.Zone, bin.Aisle, bin.Shelf, bin.IsActive, 0, bin.LastCountedAt);
    }
}
