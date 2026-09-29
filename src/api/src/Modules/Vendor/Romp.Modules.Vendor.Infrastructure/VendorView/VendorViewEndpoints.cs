using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Vendor.Application.VendorView;

namespace Romp.Modules.Vendor.Infrastructure.VendorView;

internal static class VendorViewEndpoints
{
    public static void MapVendorViewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/purchase-orders").WithTags("Vendor");

        // SCRUM-93 task 43. 404 for a missing or never-sent PO - a vendor can't tell those apart.
        group.MapGet("/{id:long}/vendor-view", async (long id, ISender sender, CancellationToken cancellationToken) =>
        {
            var view = await sender.Send(new GetVendorFacingPoViewQuery(id), cancellationToken);
            return view is null ? Results.NotFound() : Results.Ok(view);
        });
    }
}
