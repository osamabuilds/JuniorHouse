using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Application.Abstractions;
using Romp.Modules.Reference.Domain.Lookups;

namespace Romp.Modules.Reference.Application.Lookups;

/// <summary>
/// AC-1, AC-2: lists rows for one lookup type, active-only by default (<paramref name="IncludeInactive"/>
/// for the admin screen, which still needs to show retired values for context).
/// </summary>
public sealed record ListLookupQuery<TLookup>(bool IncludeInactive = false) : IRequest<IReadOnlyList<LookupDto>>
    where TLookup : Lookup;

public sealed class ListLookupQueryHandler<TLookup>(IReferenceDbContext dbContext)
    : IRequestHandler<ListLookupQuery<TLookup>, IReadOnlyList<LookupDto>>
    where TLookup : Lookup
{
    public async Task<IReadOnlyList<LookupDto>> Handle(ListLookupQuery<TLookup> request, CancellationToken cancellationToken)
    {
        var query = dbContext.Lookups<TLookup>().AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(lookup => lookup.IsActive);
        }

        var lookups = await query
            .OrderBy(lookup => lookup.SortSeq)
            .ThenBy(lookup => lookup.Name)
            .ToListAsync(cancellationToken);

        return lookups.Select(lookup => lookup.ToDto()).ToList();
    }
}
