using MediatR;
using Romp.BuildingBlocks.Application.Paging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Vendor.Application.Vendors;

namespace Romp.Modules.Vendor.Infrastructure.Vendors;

internal static class VendorEndpoints
{
    public static void MapVendorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vendors").WithTags("Vendor");

        group.MapPost("/", async (CreateVendorCommand command, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(command, cancellationToken)));

        group.MapPut("/{id:long}", async (
                long id,
                UpdateVendorRequest body,
                ISender sender,
                CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(body.ToCommand(id), cancellationToken)));

        group.MapGet("/{id:long}", async (long id, ISender sender, CancellationToken cancellationToken) =>
        {
            var vendor = await sender.Send(new GetVendorByIdQuery(id), cancellationToken);
            return vendor is null ? Results.NotFound() : Results.Ok(vendor);
        });

        group.MapGet("/", async (
                string? search,
                short? cityId,
                short? specialisationId,
                ISender sender,
                CancellationToken cancellationToken,
                bool activeOnly = true,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(
                new SearchVendorsQuery(search, cityId, specialisationId, activeOnly, page, pageSize),
                cancellationToken)));
    }
}
