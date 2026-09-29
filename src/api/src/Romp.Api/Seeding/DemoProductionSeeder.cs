using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Infrastructure.Persistence;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Infrastructure.Persistence;

namespace Romp.Api.Seeding;

/// <summary>
/// Sprint 3 (SCRUM-194): one demo purchase order in each production state, so the "Try Sprint 3" walkthrough has something
/// to look at without walking a PO through every step first. Written through the same domain methods the API uses (so each
/// carries the events and audit stamps a real one would), except that the run is created here rather than by the acknowledge
/// event - the dispatcher does not run yet at seeding time. Idempotent: skips when the first demo PO exists. Development only.
/// </summary>
internal static class DemoProductionSeeder
{
    private const string FirstDemoPoNo = "PO-2026-90002";
    private const string Person = "Demo Buyer";
    private const string Reporter = "Ali Raza";
    private const short RecordedLateDays = 3;

    // Pakistan has no daylight saving: a fixed +05:00 offset gives the same "today" the API uses for its date bounds.
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    public static async Task SeedAsync(
        CatalogDbContext catalogContext, VendorDbContext vendorContext, ProductionDbContext productionContext, CancellationToken cancellationToken)
    {
        if (await vendorContext.PurchaseOrders.AnyAsync(po => po.PoNo == FirstDemoPoNo, cancellationToken))
        {
            return;
        }

        var style = await catalogContext.Styles.FirstOrDefaultAsync(s => s.Code == "STY-DRESS-001", cancellationToken);
        var vendor = await vendorContext.Vendors.OrderBy(v => v.Id).FirstOrDefaultAsync(cancellationToken);
        if (style is null || vendor is null)
        {
            return;
        }

        // 1. Acknowledged, run open, nothing recorded yet.
        await AcknowledgedWithRunAsync(vendorContext, productionContext, style.Id, vendor.Id, "PO-2026-90002", cancellationToken);

        // 2. Sample rejected once, then approved: bulk cutting can be recorded.
        var (_, run) = await AcknowledgedWithRunAsync(vendorContext, productionContext, style.Id, vendor.Id, "PO-2026-90003", cancellationToken);
        run.SubmitPpSample(Today, Reporter, Person, "First sample: collar looks wide.", Today, RecordedLateDays);
        await productionContext.SaveChangesAsync(cancellationToken);
        run.DecidePpSample(1, approve: false, Person, "Collar is 1 cm too wide against the tech pack.", Today, DateTimeOffset.UtcNow);
        run.SubmitPpSample(Today, Reporter, Person, "Second sample with the collar fixed.", Today, RecordedLateDays);
        await productionContext.SaveChangesAsync(cancellationToken);
        run.DecidePpSample(2, approve: true, Person, null, Today, DateTimeOffset.UtcNow);
        await productionContext.SaveChangesAsync(cancellationToken);

        // 3. In production: sample approved, bulk cutting started, expected completion set.
        var (po3, run3) = await AcknowledgedWithRunAsync(vendorContext, productionContext, style.Id, vendor.Id, "PO-2026-90004", cancellationToken);
        await StartProductionAsync(vendorContext, productionContext, po3, run3, cancellationToken);

        // 4. Partially delivered: cutting started, first delivery note does not complete the order.
        var (po4, run4) = await AcknowledgedWithRunAsync(vendorContext, productionContext, style.Id, vendor.Id, "PO-2026-90005", cancellationToken);
        await StartProductionAsync(vendorContext, productionContext, po4, run4, cancellationToken);
        run4.RecordDeliveryNote("DN-DEMO-1", Today, false, [new NoteLineInput(4, 1, 30), new NoteLineInput(4, 2, 20), new NoteLineInput(5, 1, 15)], TermsOf(po4), Person, Today);
        po4.MarkPartiallyDelivered();
        await SaveBothAsync(vendorContext, productionContext, cancellationToken);

        // 5. Delivered short with a final shipment: the short flag is on the note, and the order is Delivered (closing is manual).
        var (po5, run5) = await AcknowledgedWithRunAsync(vendorContext, productionContext, style.Id, vendor.Id, "PO-2026-90006", cancellationToken);
        await StartProductionAsync(vendorContext, productionContext, po5, run5, cancellationToken);
        run5.RecordDeliveryNote("DN-DEMO-1", Today, false, [new NoteLineInput(4, 1, 50), new NoteLineInput(4, 2, 40)], TermsOf(po5), Person, Today);
        run5.RecordDeliveryNote("DN-DEMO-2", Today, true, [new NoteLineInput(5, 1, 20), new NoteLineInput(5, 2, 10)], TermsOf(po5), Person, Today);
        po5.MarkDelivered();
        await SaveBothAsync(vendorContext, productionContext, cancellationToken);

        // 6. Cancelled after production started (reason 7), so its run is closed as cancelled.
        var (po6, run6) = await AcknowledgedWithRunAsync(vendorContext, productionContext, style.Id, vendor.Id, "PO-2026-90007", cancellationToken);
        await StartProductionAsync(vendorContext, productionContext, po6, run6, cancellationToken);
        await vendorContext.Entry(po6).Collection(p => p.Revisions).LoadAsync(cancellationToken);
        po6.Cancel(cancelReasonId: 7);
        run6.MarkCancelled();
        await SaveBothAsync(vendorContext, productionContext, cancellationToken);
    }

    /// <summary>The PO is created, sent and acknowledged; its production run is opened the way the acknowledge event would.</summary>
    private static async Task<(PurchaseOrder Po, ProductionRun Run)> AcknowledgedWithRunAsync(
        VendorDbContext vendorContext, ProductionDbContext productionContext, long styleId, long vendorId, string poNo, CancellationToken cancellationToken)
    {
        (short SizeId, short ColourId, int Qty)[] lines = [(4, 1, 50), (4, 2, 40), (5, 1, 30), (5, 2, 25)];
        var expected = Today.AddDays(21);
        var po = new PurchaseOrder(poNo, vendorId, styleId, unitCost: 450m, expected, paymentTermId: 2, advancePercent: 40m, lines);
        vendorContext.PurchaseOrders.Add(po);
        await vendorContext.SaveChangesAsync(cancellationToken);

        po.RaiseCreated();
        po.UpdateDraftDetails(
            450m, expected, 2, 40m, latestAcceptableDate: expected.AddDays(14), overTolerancePercent: 5m, underTolerancePercent: 5m,
            fabricResponsibilityId: 1, lines);
        po.Send();
        await vendorContext.SaveChangesAsync(cancellationToken);
        po.Acknowledge();
        await vendorContext.SaveChangesAsync(cancellationToken);

        var run = new ProductionRun(po.Id, po.PoNo, po.FabricResponsibilityId, Today, po.LatestAcceptableDate);
        productionContext.Runs.Add(run);
        await productionContext.SaveChangesAsync(cancellationToken);
        return (po, run);
    }

    private static async Task StartProductionAsync(
        VendorDbContext vendorContext, ProductionDbContext productionContext, PurchaseOrder po, ProductionRun run, CancellationToken cancellationToken)
    {
        run.SubmitPpSample(Today, Reporter, Person, "Pre-production sample.", Today, RecordedLateDays);
        await productionContext.SaveChangesAsync(cancellationToken);
        run.DecidePpSample(1, approve: true, Person, null, Today, DateTimeOffset.UtcNow);
        run.RecordMilestone(MilestoneType.FabricBooked, Today, Reporter, Person, "Fabric booked with the mill.", Today, RecordedLateDays);
        run.RecordMilestone(MilestoneType.BulkCuttingStarted, Today, Reporter, Person, null, Today, RecordedLateDays);
        run.SetExpectedCompletion(po.ExpectedDeliveryDate, "Vendor's first estimate.", Person);
        po.StartProduction();
        await SaveBothAsync(vendorContext, productionContext, cancellationToken);
    }

    private static async Task SaveBothAsync(VendorDbContext vendorContext, ProductionDbContext productionContext, CancellationToken cancellationToken)
    {
        await vendorContext.SaveChangesAsync(cancellationToken);
        await productionContext.SaveChangesAsync(cancellationToken);
    }

    private static DeliveryTerms TermsOf(PurchaseOrder po) => new(
        RevisionNumber: 0, po.ExpectedDeliveryDate, po.LatestAcceptableDate, po.OverTolerancePercent, po.UnderTolerancePercent,
        po.Lines.Select(l => new OrderedLine(l.SizeId, l.ColourId, l.Qty)).ToList());
}
