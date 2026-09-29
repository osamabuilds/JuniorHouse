using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Vendor.Tests.Persistence;

/// <summary>
/// SCRUM-92: migrating a real PostgreSQL database creates the VNDR purchase-order tables. Uses
/// Testcontainers (CLAUDE.md); Windows Application Control blocks Docker-backed tests on this
/// machine (CLAUDE.md) - written and build-verified here, run on CI.
/// </summary>
public sealed class PoMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_CreatesPurchaseOrderTables()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var context = new VendorDbContext(options);
        await context.Database.MigrateAsync();

        var po = new Domain.PurchaseOrders.PurchaseOrder(
            "PO-2026-00001", vendorId: 1, styleId: 1, unitCost: 100m,
            expectedDeliveryDate: new DateOnly(2026, 12, 1), paymentTermId: 1, advancePercent: 50m,
            lines: [(1, 1, 10)]);
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        po.Send();
        await context.SaveChangesAsync();

        var reloaded = await context.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.StatusHistory)
            .SingleAsync(p => p.Id == po.Id);

        Assert.Single(reloaded.Lines);
        Assert.Equal(2, reloaded.StatusHistory.Count);
        Assert.Equal(2, reloaded.StatusId);
    }

    /// <summary>SCRUM-93 task 16 (AC-7): the 4 new commercial-terms columns are additive and nullable - an existing (or freshly-created, pre-task-17) PO never has a value invented for them.</summary>
    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task Migrate_AddsCommercialTermsColumns_ExistingRowsNull()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var context = new VendorDbContext(options);
        await context.Database.MigrateAsync();

        var po = new Domain.PurchaseOrders.PurchaseOrder(
            "PO-2026-00003", vendorId: 1, styleId: 1, unitCost: 100m,
            expectedDeliveryDate: new DateOnly(2026, 12, 1), paymentTermId: 1, advancePercent: 50m,
            lines: [(1, 1, 10)]);
        context.PurchaseOrders.Add(po);
        await context.SaveChangesAsync();

        var reloaded = await context.PurchaseOrders.SingleAsync(p => p.Id == po.Id);

        Assert.Null(reloaded.LatestAcceptableDate);
        Assert.Null(reloaded.OverTolerancePercent);
        Assert.Null(reloaded.UnderTolerancePercent);
        Assert.Null(reloaded.FabricResponsibilityId);
    }
}
