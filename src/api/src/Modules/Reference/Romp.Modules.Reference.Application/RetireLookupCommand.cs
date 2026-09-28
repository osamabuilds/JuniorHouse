using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Domain;

namespace Romp.Modules.Reference.Application;

/// <summary>AC-2: retires a row (ACT_IND = false) - excluded from new selection, kept for history.</summary>
public sealed record RetireLookupCommand<TLookup>(short Id) : IRequest, IReferenceCommand
    where TLookup : Lookup;

public sealed class RetireLookupCommandHandler<TLookup>(IReferenceDbContext dbContext)
    : IRequestHandler<RetireLookupCommand<TLookup>>
    where TLookup : Lookup
{
    public async Task Handle(RetireLookupCommand<TLookup> request, CancellationToken cancellationToken)
    {
        var lookup = await dbContext.Lookups<TLookup>().FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"{typeof(TLookup).Name} {request.Id} was not found.");

        lookup.Retire();
    }
}
