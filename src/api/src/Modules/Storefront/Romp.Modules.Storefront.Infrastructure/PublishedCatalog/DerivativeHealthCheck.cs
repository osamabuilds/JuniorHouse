using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.Persistence;

namespace Romp.Modules.Storefront.Infrastructure.PublishedCatalog;

/// <summary>
/// Readiness check for the image pipeline (published-catalog task 19). Failed derivatives never take the host out of rotation: the
/// result is <c>Healthy</c> with none and <c>Degraded</c> with some, which <c>/health/ready</c> still answers 200 to, and the count is in
/// the result data for operators. It never returns <c>Unhealthy</c>, not even when the count cannot be read.
/// </summary>
internal sealed class DerivativeHealthCheck(StfrDbContext db) : IHealthCheck
{
    public const string Name = "storefront-derivatives";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var failed = await db.Derivatives.AsNoTracking().CountAsync(d => d.Status == DerivativeStatus.Failed, cancellationToken);
            var data = new Dictionary<string, object> { ["failedDerivatives"] = failed };
            return failed == 0
                ? HealthCheckResult.Healthy("No failed image derivatives.", data)
                : HealthCheckResult.Degraded($"{failed} image derivative(s) failed; see ERR_CODE on STFR.IMG_DRV.", data: data);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Reporting only: readiness is for the database and the like, not for this signal, so a read error is degraded, never unhealthy.
            return HealthCheckResult.Degraded("The failed-derivative count could not be read.", ex);
        }
    }
}
