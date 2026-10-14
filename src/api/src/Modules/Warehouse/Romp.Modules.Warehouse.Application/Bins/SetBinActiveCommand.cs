using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Warehouse.Application.Abstractions;

namespace Romp.Modules.Warehouse.Application.Bins;

/// <summary>Activates or deactivates a bin (BIN-4). A bin that holds stock cannot be deactivated; setting the current state is a no-op.</summary>
public sealed record SetBinActiveCommand(long BinId, bool Active, string ActingPerson) : IRequest<BinRowDto>, IWarehouseCommand;

public sealed class SetBinActiveCommandValidator : AbstractValidator<SetBinActiveCommand>
{
    public SetBinActiveCommandValidator()
    {
        RuleFor(c => c.BinId).GreaterThan(0);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class SetBinActiveCommandHandler(IWarehouseDbContext dbContext, IBinStockQuery stock)
    : IRequestHandler<SetBinActiveCommand, BinRowDto>
{
    public async Task<BinRowDto> Handle(SetBinActiveCommand request, CancellationToken cancellationToken)
    {
        var bin = await dbContext.Bins.FirstOrDefaultAsync(b => b.Id == request.BinId, cancellationToken)
            ?? throw new KeyNotFoundException($"Bin {request.BinId} was not found.");

        var units = (await stock.GetUnitsOnHandAsync([bin.Id], cancellationToken)).GetValueOrDefault(bin.Id);
        bin.SetActive(request.Active, units);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new BinRowDto(bin.Id, bin.BinCode, bin.Zone, bin.Aisle, bin.Shelf, bin.IsActive, units, bin.LastCountedAt);
    }
}
