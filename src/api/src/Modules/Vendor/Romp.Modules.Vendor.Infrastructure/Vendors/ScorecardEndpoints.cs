using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Vendors;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Vendor.Infrastructure.Vendors;

// No authentication until Sprint 8 (SEC-08, SEC-05), so no role or ownership check applies yet. Tagged "Vendor" for that sweep.
internal static class ScorecardEndpoints
{
    public static void MapScorecardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/vendors/{id:long}/scorecard", async (long id, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetVendorScorecardQuery(id), cancellationToken)))
            .WithTags("Vendor");

        // Received against ordered for one PO (Sprint 4, AC-40): shown on the PO screen.
        endpoints.MapGet("/api/purchase-orders/{id:long}/receipts", async (long id, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetPoReceiptProgressQuery(id), cancellationToken)))
            .WithTags("Vendor");
    }
}

/// <summary>The read-only scorecard question for other modules (ADR 0002): a thin wrapper over the same query the screen uses.</summary>
public sealed class VendorScorecardQueries(ISender sender) : IVendorScorecardQueries
{
    public async Task<VendorScorecard?> GetScorecardAsync(long vendorId, CancellationToken cancellationToken)
    {
        try
        {
            return await sender.Send(new GetVendorScorecardQuery(vendorId), cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}
