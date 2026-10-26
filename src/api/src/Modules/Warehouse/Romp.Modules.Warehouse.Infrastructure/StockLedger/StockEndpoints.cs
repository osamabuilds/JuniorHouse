using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Warehouse.Application.StockLedger;

namespace Romp.Modules.Warehouse.Infrastructure.StockLedger;

// Mapped through MapSecuredGroup (ADR 0011). Every route here is a GET: stock only ever changes through putaway, adjustments and
// cycle counts, and a client can never send a total or a balance (LED-10).
internal static class StockEndpoints
{
    public static void MapStockEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapSecuredGroup("/api/warehouse/stock", Permissions.InventoryRead).WithTags("Warehouse");

        group.MapGet("", async (
                string? search,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new SearchStockQuery(search, page, pageSize), cancellationToken)));

        group.MapGet("/by-bin/{binId:long}", async (
                long binId,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new GetBinContentsQuery(binId, page, pageSize), cancellationToken)));

        group.MapGet("/movements", async (
                long? variantId,
                long? binId,
                short? kindId,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = PageRequest.DefaultPageSize) =>
            Results.Ok(await sender.Send(new GetMovementsQuery(variantId, binId, kindId, page, pageSize), cancellationToken)));

        // The books-agree check (LED-2) is an administrative action.
        endpoints.MapSecuredGroup("/api/warehouse/stock", Permissions.OpsAdmin).WithTags("Warehouse")
            .MapGet("/consistency", async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new StockConsistencyQuery(), cancellationToken)));
    }
}
