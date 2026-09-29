using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.Vendors;

public sealed record GetVendorByIdQuery(long Id) : IRequest<VendorDto?>;

public sealed class GetVendorByIdQueryHandler(IVendorDbContext dbContext) : IRequestHandler<GetVendorByIdQuery, VendorDto?>
{
    public async Task<VendorDto?> Handle(GetVendorByIdQuery request, CancellationToken cancellationToken)
    {
        var vendor = await dbContext.Vendors
            .AsNoTracking()
            .Include(v => v.Specialisations)
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);

        return vendor?.ToDto();
    }
}
