using Microsoft.Extensions.Diagnostics.HealthChecks;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.PublishedCatalog;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Published-catalog task 19: the readiness check reports failed derivatives without failing readiness.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class DerivativeHealthCheckTests(StorefrontPostgres postgres)
{
    private static Task<HealthCheckResult> CheckAsync(StorefrontHost host) =>
        host.QueryAsync(db => new DerivativeHealthCheck(db).CheckHealthAsync(new HealthCheckContext()));

    [Fact]
    [Trait("Spec", "PCT-7")]
    public async Task Check_NoFailedDerivatives_IsHealthy()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var result = await CheckAsync(host);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(0, result.Data["failedDerivatives"]);
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public async Task Check_FailedDerivatives_AreCountedAndDegradeButDoNotFail()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        host.Storage.Files["images/a.jpg"] = [1, 2, 3];
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(
            variants: [Snapshots.Variant(11, "SKU-A")], images: [Snapshots.Image(101, 1)])));
        host.Resizer.Rejection = new ImageRejectedException("NOT_AN_IMAGE", "not an image");
        await host.SendAsync(new ProcessDerivativeCommand());
        await host.SendAsync(new ProcessDerivativeCommand());

        var result = await CheckAsync(host);

        Assert.Equal(HealthStatus.Degraded, result.Status); // Degraded still answers 200 on /health/ready
        Assert.Equal(2, result.Data["failedDerivatives"]);
    }
}
