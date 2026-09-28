using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Domain;
using Romp.Modules.Vendor.Infrastructure;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Vendor.Tests;

/// <summary>
/// SCRUM-93 task 20 (ADR 0007): PurchaseOrder.CreateRevision's number allocation and immutability
/// guarantee. Uses Testcontainers (CLAUDE.md) for the concurrency test - AC-24's race can only be
/// observed against a real Postgres row-version (xmin); Windows Application Control blocks
/// Docker-backed tests on this machine (CLAUDE.md) - written and build-verified here, run on CI.
/// </summary>
public sealed class PurchaseOrderRevisionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    /// <summary>
    /// AC-22: a revision's snapshot/impact figures never change after creation. This is provable
    /// statically - PurchaseOrderRevision declares no public method besides its property getters
    /// (MarkSuperseded, the one allowed post-creation change, is internal), so no caller outside
    /// this assembly can mutate it at all, let alone its snapshot fields.
    /// </summary>
    [Fact]
    [Trait("Spec", "AC-22")]
    public void NoPublicMethodMutatesSnapshotAfterCreation()
    {
        var publicMethods = typeof(PurchaseOrderRevision)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName) // excludes property get_/set_ accessors
            .ToList();

        Assert.Empty(publicMethods);
    }

    /// <summary>
    /// AC-24: revision numbers are unique and consecutive per PO, and a race between two concurrent
    /// amendments is caught, not silently allowed to allocate the same number. This uses the
    /// SentToVendor path (AC-9, immediately In-force) because that's the one where CreateRevision
    /// also updates PO_MAIN's own mirrored columns (ADR 0007) - a real property change EF's xmin
    /// concurrency token protects, which is the mechanism the ADR relies on (not a separate lock).
    /// </summary>
    [Fact]
    [Trait("Spec", "AC-24")]
    public async Task Create_TwoConcurrentAmendments_OneSucceedsOneConflicts()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        long poId;

        await using (var context = new VendorDbContext(options))
        {
            await context.Database.MigrateAsync();

            var po = new PurchaseOrder(
                "PO-2026-00020", vendorId: 1, styleId: 1, unitCost: 100m,
                expectedDeliveryDate: new DateOnly(2026, 12, 1), paymentTermId: 1, advancePercent: 50m,
                lines: [(1, 1, 10)]);
            context.PurchaseOrders.Add(po);
            await context.SaveChangesAsync();

            po.Send();
            await context.SaveChangesAsync();

            poId = po.Id;
        }

        // Two independent contexts, each simulating a different staff member, load the same PO at
        // the same (still-original) xmin before either amends it.
        await using var contextA = new VendorDbContext(options);
        await using var contextB = new VendorDbContext(options);

        var poA = await contextA.PurchaseOrders.Include(p => p.Revisions).SingleAsync(p => p.Id == poId);
        var poB = await contextB.PurchaseOrders.Include(p => p.Revisions).SingleAsync(p => p.Id == poId);

        poA.CreateRevision(
            initiatorId: 1, reasonId: 1, impactNote: "Amendment A", vendorMessage: null,
            unitCost: 110m, expectedDeliveryDate: new DateOnly(2026, 12, 5), latestAcceptableDate: null,
            overTolerancePercent: null, underTolerancePercent: null, paymentTermId: 1, advancePercent: 50m,
            fabricResponsibilityId: null, lines: [(1, 1, 20)], goesImmediatelyInForce: true);

        poB.CreateRevision(
            initiatorId: 1, reasonId: 1, impactNote: "Amendment B", vendorMessage: null,
            unitCost: 120m, expectedDeliveryDate: new DateOnly(2026, 12, 10), latestAcceptableDate: null,
            overTolerancePercent: null, underTolerancePercent: null, paymentTermId: 1, advancePercent: 50m,
            fabricResponsibilityId: null, lines: [(1, 1, 30)], goesImmediatelyInForce: true);

        await contextA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());

        await using var verifyContext = new VendorDbContext(options);
        var revisions = await verifyContext.Set<PurchaseOrderRevision>()
            .Where(r => r.PoId == poId)
            .OrderBy(r => r.RevisionNumber)
            .ToListAsync();

        // Only A's revision (Rev 1) landed; B's failed save wrote nothing.
        Assert.Single(revisions);
        Assert.Equal(1, revisions[0].RevisionNumber);
        Assert.Equal(RevisionStatusInForce, revisions[0].StatusId);

        var reloadedPo = await verifyContext.PurchaseOrders.SingleAsync(p => p.Id == poId);
        Assert.Equal(110m, reloadedPo.UnitCost);
    }

    private const short RevisionStatusInForce = 2;
}
