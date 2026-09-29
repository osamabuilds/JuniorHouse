using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;

namespace Romp.Modules.Production.Tests.Support;

/// <summary>Builds aggregates in the states the domain tests need. PO sent 2026-09-01, today 2026-09-29 (Pakistan time).</summary>
internal static class Runs
{
    public static readonly DateOnly Sent = new(2026, 9, 1);
    public static readonly DateOnly Today = new(2026, 9, 29);
    public const string Person = "Osama Shafique";
    public const string Reporter = "Ali Raza";
    public const int LateDays = 3;

    public static ProductionRun New(short? fabric = FabricResponsibility.VendorSupplied, DateOnly? latestAcceptable = null) =>
        new(poId: 1, "PO-2026-00001", fabric, Sent, latestAcceptable ?? new DateOnly(2026, 10, 25));

    public static ProductionRun WithApprovedSample(short? fabric = FabricResponsibility.VendorSupplied)
    {
        var run = New(fabric);
        run.SubmitPpSample(new DateOnly(2026, 9, 5), Reporter, Person, null, Today, LateDays);
        run.DecidePpSample(1, approve: true, Person, null, Today, DateTimeOffset.UtcNow);
        return run;
    }

    public static ProductionRun InProduction(short? fabric = FabricResponsibility.VendorSupplied, DateOnly? cutting = null)
    {
        var run = WithApprovedSample(fabric);
        if (fabric == FabricResponsibility.RompSupplied)
        {
            Record(run, MilestoneType.FabricDispatchedByRomp, new DateOnly(2026, 9, 8));
        }

        Record(run, MilestoneType.BulkCuttingStarted, cutting ?? new DateOnly(2026, 9, 10));
        return run;
    }

    public static Milestone Record(ProductionRun run, short type, DateOnly claimed, DateOnly? today = null) =>
        run.RecordMilestone(type, claimed, Reporter, Person, null, today ?? Today, LateDays);

    public static DeliveryTerms Terms(
        short revision = 0, DateOnly? expected = null, DateOnly? latest = null, decimal? over = 5m, decimal? under = 5m) => new(
        revision, expected ?? new DateOnly(2026, 10, 15), latest ?? new DateOnly(2026, 10, 25), over, under,
        [new OrderedLine(1, 10, 100), new OrderedLine(2, 10, 50)]);

    public static IReadOnlyList<NoteLineInput> Lines(int size1, int size2) =>
        new[] { new NoteLineInput(1, 10, size1), new NoteLineInput(2, 10, size2) }.Where(l => l.Qty > 0).ToList();

    public static DeliveryNote Deliver(
        ProductionRun run, string dnNo, int size1, int size2, bool final = false, DateOnly? dispatch = null, DeliveryTerms? terms = null) =>
        run.RecordDeliveryNote(dnNo, dispatch ?? new DateOnly(2026, 9, 25), final, Lines(size1, size2), terms ?? Terms(), Person, Today);
}
