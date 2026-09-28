using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Romp.Modules.Catalog.Application;

public sealed record GetStyleByIdQuery(long Id) : IRequest<StyleDto?>;

public sealed class GetStyleByIdQueryHandler(ICatalogDbContext dbContext) : IRequestHandler<GetStyleByIdQuery, StyleDto?>
{
    public async Task<StyleDto?> Handle(GetStyleByIdQuery request, CancellationToken cancellationToken)
    {
        var style = await dbContext.Styles
            .AsNoTracking()
            .Include(s => s.Colourways)
            .Include(s => s.Sizes)
            .Include(s => s.TargetLines)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        return style?.ToDto();
    }
}
