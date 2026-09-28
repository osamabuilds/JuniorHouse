using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Catalog.Infrastructure;
using Romp.Modules.Reference.Infrastructure;
using Romp.Modules.Vendor.Infrastructure;

namespace Romp.Api.IntegrationTests;

/// <summary>
/// Proves the three Sprint-1 module DbContexts (SCRUM-162) resolve through the API host's DI
/// container without throwing. It does not touch a real database: building an EF Core DbContext
/// from its options doesn't open a connection, and <see cref="RompApiWebApplicationFactory"/>'s
/// "Testing" environment skips the dev-only startup migration that would need one.
/// </summary>
public sealed class RompApiHostTests(RompApiWebApplicationFactory factory)
    : IClassFixture<RompApiWebApplicationFactory>
{
    [Fact]
    [Trait("Spec", "SCRUM-162")]
    public void Startup_ConfiguresThreeDbContexts_NoException()
    {
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ReferenceDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CatalogDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<VendorDbContext>());
    }
}
