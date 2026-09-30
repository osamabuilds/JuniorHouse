using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Infrastructure.Persistence;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.InboundDeliveries;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.Inspections.Aql;
using Romp.Modules.Quality.Domain.Outcomes;
using Romp.Modules.Quality.Domain.Receiving;
using Romp.Modules.Quality.Domain.SafetyChecks;
using Romp.Modules.Quality.Infrastructure.Persistence;
using Romp.Modules.Vendor.Infrastructure.Persistence;

namespace Romp.Api.Seeding;

/// <summary>
/// Sprint 4 (SCRUM-197): nine delivered purchase orders, one for each receiving and QC state, so the "Try Sprint 4" walkthrough has
/// every screen to look at without receiving and inspecting a delivery first: a count not inspected yet, a short count, a
/// concession, a rejection, goods waiting for rework, a rework that came back and was accepted, a split into a passing and a
/// rejected part, a failed child-safety check, and an over-delivery. Written through the same domain methods the API uses, so each
/// carries the events and audit stamps a real one would. The QC copy of each delivery note is written here as well, because the
/// dispatcher does not run yet at seeding time; when it does, the consumer finds the copy and does nothing. Idempotent: skips when
/// the first demo PO exists. Development only.
/// </summary>
internal static class DemoQualitySeeder
{
    private const string FirstDemoPoNo = "PO-2026-91001";
    private const string Receiver = "Demo Receiver";
    private const string Inspector = "Demo Inspector";

    private static readonly AqlPlanTable PlanTable = new(AqlSeedData.SampleRows(), AqlSeedData.LimitRows());

    // Every demo PO orders these four lines (size, colour, qty) and the vendor delivers all 145 on one final delivery note.
    private static readonly (short SizeId, short ColourId, int Qty)[] Ordered = [(4, 1, 50), (4, 2, 40), (5, 1, 30), (5, 2, 25)];

    private static DateOnly Today => DemoProductionSeeder.Today;

    public static async Task SeedAsync(
        CatalogDbContext catalogContext,
        VendorDbContext vendorContext,
        ProductionDbContext productionContext,
        QcDbContext qualityContext,
        CancellationToken cancellationToken)
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

        var context = new Context(catalogContext, vendorContext, productionContext, qualityContext, style.Id, vendor.Id, cancellationToken);

        // 1. Counted and matched, not inspected yet.
        await context.ReceiveAsync(1, counted: Ordered);

        // 2. Counted short (five pieces did not arrive), inspected, both safety checks passed, accepted.
        var short2 = await context.ReceiveAsync(2, counted: [(4, 1, 50), (4, 2, 35), (5, 1, 30), (5, 2, 25)]);
        await context.InspectAsync(short2, defects: []);
        await context.SafetyAsync(short2, new SafetyRow(SafetyType.ChokingHazardTrims, CheckResult.Pass, [], null), new SafetyRow(SafetyType.DrawstringNeckline, CheckResult.NotApplicable, [], "Dress has no drawstring."));
        await context.DecideAsync(short2, [new DecisionPart(QcOutcome.Accepted, null, Lines(short2))]);

        // 3. Minor defects, accepted with a concession.
        var concession = await context.ReceiveAsync(3, counted: Ordered);
        await context.InspectAsync(concession, defects: [new DefectInput(DefectType.FabricColour, DefectSeverity.Minor, 1, null, null, "Slight shade variation on one piece.")]);
        await context.SafetyAsync(concession, PassBoth);
        await context.DecideAsync(concession, [new DecisionPart(QcOutcome.Concession, "Shade variation is minor; sold as-is.", Lines(concession))]);

        // 4. A critical defect fails the inspection; the whole batch is rejected.
        var rejected = await context.ReceiveAsync(4, counted: Ordered);
        await context.InspectAsync(rejected, defects: [new DefectInput(DefectType.TrimsSecurity, DefectSeverity.Critical, 2, null, null, "Buttons came off when pulled.")], failChecks: [CheckType.TrimsSecure]);
        await context.DecideAsync(rejected, [new DecisionPart(QcOutcome.Rejected, "Buttons are not secure.", Lines(rejected))]);

        // 5. Part of the batch failed: the passing part is accepted and released, the rest waits at the vendor for rework.
        var rework = await context.ReceiveAsync(5, counted: Ordered);
        await context.InspectAsync(rework, defects: [new DefectInput(DefectType.Stitching, DefectSeverity.Major, 2, 4, 2, "Hems come undone.")], failChecks: [CheckType.Stitching]);
        await context.SafetyAsync(rework, PassBoth);
        await context.DecideAsync(
            rework,
            [
                new DecisionPart(QcOutcome.Accepted, null, [new PartLine(4, 1, 50), new PartLine(5, 1, 30), new PartLine(5, 2, 25)]),
                new DecisionPart(QcOutcome.Rework, "Re-sew the hems.", [new PartLine(4, 2, 40)]),
            ]);

        // 6. The same, but the reworked goods came back, were re-inspected with fresh safety checks and accepted.
        var returned = await context.ReceiveAsync(6, counted: Ordered);
        await context.InspectAsync(returned, defects: [new DefectInput(DefectType.Stitching, DefectSeverity.Major, 2, 4, 2, "Hems come undone.")], failChecks: [CheckType.Stitching]);
        await context.SafetyAsync(returned, PassBoth);
        await context.DecideAsync(
            returned,
            [
                new DecisionPart(QcOutcome.Accepted, null, [new PartLine(4, 1, 50), new PartLine(5, 1, 30), new PartLine(5, 2, 25)]),
                new DecisionPart(QcOutcome.Rework, "Re-sew the hems.", [new PartLine(4, 2, 40)]),
            ]);
        await context.ReworkComesBackAcceptedAsync(returned);

        // 7. A split into a passing part and a rejected part.
        var split = await context.ReceiveAsync(7, counted: Ordered);
        await context.InspectAsync(split, defects: [new DefectInput(DefectType.VisibleDefect, DefectSeverity.Major, 2, 5, 1, "Torn seams.")], failChecks: [CheckType.VisibleDefects]);
        await context.SafetyAsync(split, PassBoth);
        await context.DecideAsync(
            split,
            [
                new DecisionPart(QcOutcome.Accepted, null, [new PartLine(4, 1, 50), new PartLine(4, 2, 40), new PartLine(5, 2, 25)]),
                new DecisionPart(QcOutcome.Rejected, "Torn seams.", [new PartLine(5, 1, 30)]),
            ]);

        // 8. The choking-hazard check failed for the size 5 lines: they can only be rejected; size 4 is accepted.
        var safety = await context.ReceiveAsync(8, counted: Ordered);
        await context.InspectAsync(safety, defects: []);
        await context.SafetyAsync(safety, new SafetyRow(SafetyType.ChokingHazardTrims, CheckResult.Fail, [new LineRef(5, 1), new LineRef(5, 2)], null), new SafetyRow(SafetyType.DrawstringNeckline, CheckResult.Pass, [], null));
        await context.DecideAsync(
            safety,
            [
                new DecisionPart(QcOutcome.Accepted, null, [new PartLine(4, 1, 50), new PartLine(4, 2, 40)]),
                new DecisionPart(QcOutcome.Rejected, "Bows on the size 5 dresses are a choking hazard.", [new PartLine(5, 1, 30), new PartLine(5, 2, 25)]),
            ]);

        // 9. More arrived than the delivery note says: shown as over-delivery, in inspection with the safety checks still to record.
        var over = await context.ReceiveAsync(9, counted: [(4, 1, 55), (4, 2, 40), (5, 1, 30), (5, 2, 25)]);
        await context.InspectAsync(over, defects: []);
    }

    private sealed record SafetyRow(short Type, short Result, LineRef[] Lines, string? Reason);

    private static readonly SafetyRow[] PassBoth =
    [
        new SafetyRow(SafetyType.ChokingHazardTrims, CheckResult.Pass, [], null),
        new SafetyRow(SafetyType.DrawstringNeckline, CheckResult.Pass, [], null),
    ];

    private static PartLine[] Lines(Scenario scenario) => scenario.Batch.Lines.Select(l => new PartLine(l.SizeId, l.ColourId, l.Qty)).ToArray();

    private sealed record Scenario(PurchaseOrderInfo Po, QcBatch Batch);

    private sealed record PurchaseOrderInfo(long Id, string PoNo);

    private sealed class Context(
        CatalogDbContext catalog,
        VendorDbContext vendors,
        ProductionDbContext production,
        QcDbContext quality,
        long styleId,
        long vendorId,
        CancellationToken cancellationToken)
    {
        public async Task<Scenario> ReceiveAsync(int number, (short SizeId, short ColourId, int Qty)[] counted)
        {
            _ = catalog; // the catalog is only needed to find the style; kept so every seeder takes the same contexts
            var poNo = $"PO-2026-{91000 + number}";
            var (po, run) = await DemoProductionSeeder.AcknowledgedWithRunAsync(vendors, production, styleId, vendorId, poNo, cancellationToken);
            await DemoProductionSeeder.StartProductionAsync(vendors, production, po, run, cancellationToken);

            var dnNo = $"DN-QC-{number}";
            run.RecordDeliveryNote(
                dnNo, Today, true, Ordered.Select(l => new NoteLineInput(l.SizeId, l.ColourId, l.Qty)).ToList(),
                DemoProductionSeeder.TermsOf(po), DemoProductionSeeder.Person, Today);
            await DemoProductionSeeder.SaveBothAsync(vendors, production, cancellationToken);

            var delivery = new InboundDelivery(run.Id, po.Id, po.PoNo, dnNo, Today, true, Ordered.Select(l => new ClaimedLine(l.SizeId, l.ColourId, l.Qty)).ToList());
            quality.Deliveries.Add(delivery);
            await quality.SaveChangesAsync(cancellationToken);

            var receipt = GoodsReceipt.Open(
                delivery, $"GRN-2026-{90000 + number}", Today, Receiver, number == 2 ? "Five pieces missing from one carton." : null,
                counted.Select(l => new CountedLine(l.SizeId, l.ColourId, l.Qty)).ToList(), new HashSet<(short, short)>(), Today, futureDays: 0);
            quality.GoodsReceipts.Add(receipt);
            await quality.SaveChangesAsync(cancellationToken);
            receipt.RaiseRecorded(); // the id exists now, so the event carries it

            var batch = QcBatch.OpenFor(receipt.Id, po.Id, po.PoNo, receipt.Lines.Select(l => (l.SizeId, l.ColourId, l.CountedQty)))!;
            quality.Batches.Add(batch);
            await quality.SaveChangesAsync(cancellationToken);
            return new Scenario(new PurchaseOrderInfo(po.Id, po.PoNo), batch);
        }

        public async Task InspectAsync(Scenario scenario, IReadOnlyList<DefectInput> defects, short[]? failChecks = null)
        {
            var batch = scenario.Batch;
            var plan = AqlCalculator.Plan(PlanTable, batch.LotQty, InspectionLevel.II, AqlLevel.Aql250, fullInspectionMaxLot: 50);
            batch.RecordInspection(
                new InspectionInput(
                    null, Today, Inspector, null, InspectionLevel.II, AqlLevel.Aql250,
                    CheckType.All.Select(t => new CheckInput(t, failChecks?.Contains(t) == true ? CheckResult.Fail : CheckResult.Pass)).ToList(),
                    defects),
                plan, criticalTolerance: 0, Today);
            await quality.SaveChangesAsync(cancellationToken);
        }

        public async Task SafetyAsync(Scenario scenario, params SafetyRow[] rows)
        {
            var inspection = scenario.Batch.Inspections.OrderBy(i => i.Id).First(i => i.SubBatchId is null);
            scenario.Batch.RecordSafetyChecks(
                inspection.Id,
                rows.Select(r => new SafetyInput(r.Type, r.Result, r.Reason, r.Lines)).ToList(),
                Inspector);
            await quality.SaveChangesAsync(cancellationToken);
        }

        public async Task DecideAsync(Scenario scenario, IReadOnlyList<DecisionPart> parts)
        {
            var created = scenario.Batch.Decide(parts, Inspector, DateTimeOffset.UtcNow);
            await quality.SaveChangesAsync(cancellationToken); // the parts have ids now, so their events can carry them
            foreach (var sub in created)
            {
                scenario.Batch.RaiseOutcomeEvent(sub);
            }

            await quality.SaveChangesAsync(cancellationToken);
        }

        /// <summary>The rework part returns in full, is re-inspected with both safety checks recorded again, and is accepted.</summary>
        public async Task ReworkComesBackAcceptedAsync(Scenario scenario)
        {
            var batch = scenario.Batch;
            var rework = batch.SubBatches.Single(s => s.OutcomeId == QcOutcome.Rework);
            var back = batch.RecordReworkReturn(rework.Id, Today, Receiver, "Collected from the vendor.", rework.Lines.Select(l => new PartLine(l.SizeId, l.ColourId, l.Qty)).ToList(), Today);
            await quality.SaveChangesAsync(cancellationToken);

            var plan = AqlCalculator.Plan(PlanTable, batch.ScopeQty(back.Id), InspectionLevel.II, AqlLevel.Aql250, fullInspectionMaxLot: 50);
            var round = batch.RecordInspection(
                new InspectionInput(
                    back.Id, Today, Inspector, null, InspectionLevel.II, AqlLevel.Aql250,
                    CheckType.All.Select(t => new CheckInput(t, CheckResult.Pass)).ToList(), []),
                plan, criticalTolerance: 0, Today);
            await quality.SaveChangesAsync(cancellationToken);

            batch.RecordSafetyChecks(
                round.Id,
                [new SafetyInput(SafetyType.ChokingHazardTrims, CheckResult.Pass, null, []), new SafetyInput(SafetyType.DrawstringNeckline, CheckResult.Pass, null, [])],
                Inspector);
            var decided = batch.DecideSubBatch(back.Id, QcOutcome.Accepted, null, Inspector, DateTimeOffset.UtcNow);
            batch.RaiseOutcomeEvent(decided);
            await quality.SaveChangesAsync(cancellationToken);
        }
    }
}
