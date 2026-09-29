using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.PurchaseOrders;

/// <summary>
/// Configured tolerance bounds for PO commercial terms (SCRUM-93 task 17, AC-5) - "configuration,
/// not constants" per plan.md. Defaults are spec.md's suggested starting point (5% each way, 20%
/// maximum); the exact production values are still an open question there (pending vendor
/// confirmation) - not invented here, just the configurable default until that's resolved.
/// </summary>
public sealed class PoCommercialTermsOptions
{
    public decimal DefaultTolerancePercent { get; init; } = 5m;

    public decimal MaxTolerancePercent { get; init; } = 20m;
}
