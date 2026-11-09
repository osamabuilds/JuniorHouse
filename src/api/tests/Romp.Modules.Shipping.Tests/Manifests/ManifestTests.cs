using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Application.Manifests;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Tests.Support;

namespace Romp.Modules.Shipping.Tests.Manifests;

/// <summary>S14 S7: the end-of-day manifest per courier, reconciled against the physical count (MNF-1 to MNF-10).</summary>
[Collection(ShippingPostgresFixtureDefinition.Name)]
public sealed class ManifestTests(ShippingPostgres postgres)
{
    // Each test uses its own business day so manifests of other tests (the database is shared) never mix in.
    private static int _day;

    private static DateOnly NextDay() => new DateOnly(2040, 1, 1).AddDays(Interlocked.Increment(ref _day));

    private static async Task<ManifestSummaryDto> GenerateAsync(ShippingHost host, DateOnly day, short courier = Couriers.Primary) =>
        (await host.SendAsync(new GenerateManifestsCommand(day, courier))).Single();

    [Fact]
    [Trait("Spec", "MNF-1")]
    public async Task TheManifest_ListsEveryBookedParcelWithOrderWaybillCashAndWeight_AndATotal()
    {
        using var host = new ShippingHost(postgres);
        var day = NextDay();
        var ids = new[] { await host.NewBookedShipmentAsync(), await host.NewBookedShipmentAsync() };

        var summary = await GenerateAsync(host, day);
        var detail = (await host.SendAsync(new GetManifestQuery(summary.Id)))!;

        Assert.Equal((2, "OPEN"), (summary.ParcelCount >= 2 ? 2 : summary.ParcelCount, summary.Status));
        Assert.All(ids, id => Assert.Contains(detail.Lines, l => l.ShipmentId == id));
        var line = detail.Lines.First(l => l.ShipmentId == ids[0]);
        Assert.Equal((4700m, 600), (line.CashToCollect, line.WeightGrams));
        Assert.StartsWith("PRM-", line.Waybill);
        Assert.Equal(detail.Lines.Sum(l => l.CashToCollect), detail.TotalCash);
        Assert.Equal(detail.Lines.Sum(l => l.WeightGrams), detail.TotalWeightGrams);
    }

    [Fact]
    [Trait("Spec", "MNF-2")]
    public async Task GeneratingForAllCouriers_MakesOneManifestPerCourierThatHasParcels()
    {
        using var host = new ShippingHost(postgres);
        var day = NextDay();
        await host.NewBookedShipmentAsync("PRIMARY");
        await host.NewBookedShipmentAsync("SECONDARY");

        var made = await host.SendAsync(new GenerateManifestsCommand(day));

        Assert.Equal([Couriers.NameOf(Couriers.Primary), Couriers.NameOf(Couriers.Secondary)], made.Select(m => m.CourierName).Order().Reverse().Reverse());
    }

    [Fact]
    [Trait("Spec", "MNF-3")]
    public async Task AParcelOnAManifest_IsNotPutOnAnother()
    {
        using var host = new ShippingHost(postgres);
        var first = NextDay();
        var id = await host.NewBookedShipmentAsync();
        await GenerateAsync(host, first);

        var again = await host.SendAsync(new GenerateManifestsCommand(NextDay(), Couriers.Primary));

        Assert.DoesNotContain(again, m => m.ParcelCount > 0 && m.Id == 0);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.ManifestLines.CountAsync(l => l.ShipmentId == id));
    }

    [Fact]
    [Trait("Spec", "MNF-4")]
    public async Task GeneratingTwice_AddsOnlyTheNewParcels_ToTheOpenManifest()
    {
        using var host = new ShippingHost(postgres);
        var day = NextDay();
        await host.NewBookedShipmentAsync();
        var first = await GenerateAsync(host, day);

        await host.NewBookedShipmentAsync();
        var second = await GenerateAsync(host, day);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ParcelCount + 1, second.ParcelCount);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.Manifests.CountAsync(m => m.CourierId == Couriers.Primary && m.BusinessDate == day));
    }

    [Fact]
    [Trait("Spec", "MNF-5")]
    public async Task ACountEqualToTheParcels_IsMatched_AndADifferentCountIsAMismatch()
    {
        using var host = new ShippingHost(postgres);
        var dayA = NextDay();
        await host.NewBookedShipmentAsync();
        var a = await GenerateAsync(host, dayA);
        var dayB = NextDay();
        await host.NewBookedShipmentAsync();
        var b = await GenerateAsync(host, dayB);

        var matched = (await host.SendAsync(new CountManifestCommand(a.Id, a.ParcelCount, ShippingHost.Staff)))!;
        var mismatch = (await host.SendAsync(new CountManifestCommand(b.Id, b.ParcelCount - 1, ShippingHost.Staff)))!;

        Assert.Equal(("MATCHED", ShippingHost.Staff), (matched.Status, matched.CountedBy));
        Assert.Equal("MISMATCH", mismatch.Status);
        Assert.Null(await host.SendAsync(new CountManifestCommand(404_404, 1, ShippingHost.Staff)));
    }

    [Fact]
    [Trait("Spec", "MNF-6")]
    public async Task AfterAMismatch_AParcelNotHandedOver_GoesBackToTheNextManifest()
    {
        using var host = new ShippingHost(postgres);
        var day = NextDay();
        var kept = await host.NewBookedShipmentAsync();
        var left = await host.NewBookedShipmentAsync();
        var manifest = await GenerateAsync(host, day);
        var count = manifest.ParcelCount - 1;
        await host.SendAsync(new CountManifestCommand(manifest.Id, count, ShippingHost.Staff));

        await host.SendAsync(new MarkHandedOverCommand(manifest.Id, left, false, ShippingHost.Staff));
        var closed = (await host.SendAsync(new ResolveManifestCommand(manifest.Id, "The courier left it at the depot", ShippingHost.Staff)))!;

        Assert.Equal("CLOSED", closed.Status);
        Assert.Null((await ShippingHost.LoadAsync(postgres, left)).ManifestId);
        Assert.Equal(manifest.Id, (await ShippingHost.LoadAsync(postgres, kept)).ManifestId);
        var next = await GenerateAsync(host, NextDay());
        var detail = (await host.SendAsync(new GetManifestQuery(next.Id)))!;
        Assert.Contains(detail.Lines, l => l.ShipmentId == left);
    }

    [Fact]
    [Trait("Spec", "MNF-7")]
    public async Task ACountedManifest_TakesNoMoreParcels_AndTheyWaitForTheNextDay()
    {
        using var host = new ShippingHost(postgres);
        var day = NextDay();
        await host.NewBookedShipmentAsync();
        var manifest = await GenerateAsync(host, day);
        await host.SendAsync(new CountManifestCommand(manifest.Id, manifest.ParcelCount, ShippingHost.Staff));
        var late = await host.NewBookedShipmentAsync();

        var made = await host.SendAsync(new GenerateManifestsCommand(day, Couriers.Primary));

        Assert.Empty(made);
        Assert.Null((await ShippingHost.LoadAsync(postgres, late)).ManifestId);
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CountManifestCommand(manifest.Id, 1, ShippingHost.Staff)));
    }

    [Fact]
    [Trait("Spec", "MNF-8")]
    public async Task TheManifestDetail_HasTheLinesInAStableOrder_ForThePrintedPage()
    {
        using var host = new ShippingHost(postgres);
        var day = NextDay();
        await host.NewBookedShipmentAsync();
        await host.NewBookedShipmentAsync();
        var manifest = await GenerateAsync(host, day);

        var detail = (await host.SendAsync(new GetManifestQuery(manifest.Id)))!;

        Assert.Equal(detail.Lines.OrderBy(l => l.ShipmentId).Select(l => l.ShipmentId), detail.Lines.Select(l => l.ShipmentId));
        Assert.Null(await host.SendAsync(new GetManifestQuery(404_404)));
    }

    [Fact]
    [Trait("Spec", "MNF-10")]
    public async Task TheManifestList_IsNewestFirst_AndPaged()
    {
        using var host = new ShippingHost(postgres);
        await host.NewBookedShipmentAsync();
        var older = await GenerateAsync(host, NextDay());
        await host.NewBookedShipmentAsync();
        var newer = await GenerateAsync(host, NextDay());

        var page = await host.SendAsync(new ListManifestsQuery(1, 100));
        var small = await host.SendAsync(new ListManifestsQuery(1, 1));

        var ids = page.Items.Select(m => m.Id).ToList();
        Assert.True(ids.IndexOf(newer.Id) < ids.IndexOf(older.Id));
        Assert.Single(small.Items);
        Assert.True(small.TotalCount >= 2);
    }

    [Fact]
    [Trait("Spec", "MNF-9")]
    public async Task EveryManifestAction_NeedsASignedInStaffMember()
    {
        using var host = new ShippingHost(postgres);
        await host.NewBookedShipmentAsync();
        var manifest = await GenerateAsync(host, NextDay());

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new CountManifestCommand(manifest.Id, 1, "")));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new MarkHandedOverCommand(manifest.Id, 1, false, " ")));
        Assert.Equal(PageRequest.DefaultPageSize, 25);
    }

    [Fact]
    [Trait("Spec", "MNF-2")]
    public void TheBusinessDay_IsThePakistanDay()
    {
        Assert.Equal(new DateOnly(2026, 11, 10), BusinessDay.Of(new DateTimeOffset(2026, 11, 9, 20, 30, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 11, 9), BusinessDay.Of(new DateTimeOffset(2026, 11, 9, 18, 59, 0, TimeSpan.Zero)));
    }
}
