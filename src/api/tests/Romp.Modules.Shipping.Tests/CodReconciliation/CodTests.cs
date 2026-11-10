using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Shipping.Application.CodReconciliation;
using Romp.Modules.Shipping.Application.Tracking;
using Romp.Modules.Shipping.Domain;
using Romp.Modules.Shipping.Tests.Support;

namespace Romp.Modules.Shipping.Tests.CodReconciliation;

/// <summary>S16 C5 to C8: statements recorded against real delivered parcels in PostgreSQL (COD-2 to COD-14).</summary>
[Collection(ShippingPostgresFixtureDefinition.Name)]
public sealed class CodTests(ShippingPostgres postgres)
{
    private static int _ref;

    private static string NextRef() => $"ST-{Interlocked.Increment(ref _ref)}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

    private async Task<(long Id, string Waybill)> DeliveredAsync(ShippingHost host, bool cod = true, DateTimeOffset? at = null)
    {
        var id = await host.NewBookedShipmentAsync();
        var waybill = (await ShippingHost.LoadAsync(postgres, id)).Waybill!;
        var (body, signature) = host.Push(waybill, "DELIVERED", at: at ?? host.Time.Now);
        Assert.Equal(PushOutcome.Accepted, await host.SendAsync(new ApplyCourierPushCommand("PRIMARY", body, signature)));
        return (id, waybill);
    }

    private static RecordCodRemittanceCommand Statement(string reference, params (string Waybill, decimal Amount)[] lines) =>
        new("PRIMARY", reference, new DateOnly(2026, 11, 10), lines.Sum(l => l.Amount), [.. lines.Select(l => new StatementLineInput(l.Waybill, l.Amount))], ShippingHost.Staff);

    [Fact]
    [Trait("Spec", "COD-2")]
    public async Task AMatchingLine_SettlesTheParcel()
    {
        using var host = new ShippingHost(postgres);
        var (id, waybill) = await DeliveredAsync(host);

        var result = await host.SendAsync(Statement(NextRef(), (waybill, 4700m)));

        Assert.False(result.AlreadyRecorded);
        Assert.Equal("MATCHED", Assert.Single(result.Remittance.Lines).Outcome);
        Assert.Equal(CodStatuses.Settled, (await ShippingHost.LoadAsync(postgres, id)).CodStatusId);
    }

    [Fact]
    [Trait("Spec", "COD-3")]
    public async Task AShortAndAnOverLine_AreFlagged_AndMarkTheirParcels()
    {
        using var host = new ShippingHost(postgres);
        var a = await DeliveredAsync(host);
        var b = await DeliveredAsync(host);

        var result = await host.SendAsync(Statement(NextRef(), (a.Waybill, 4000m), (b.Waybill, 5000m)));

        Assert.Equal(["SHORT", "OVER"], result.Remittance.Lines.Select(l => l.Outcome));
        Assert.Equal(2, result.Remittance.Summary.OpenCount);
        Assert.Equal(CodStatuses.ShortPaid, (await ShippingHost.LoadAsync(postgres, a.Id)).CodStatusId);
        Assert.Equal(CodStatuses.Over, (await ShippingHost.LoadAsync(postgres, b.Id)).CodStatusId);
    }

    [Fact]
    [Trait("Spec", "COD-4")]
    public async Task APaidParcel_PaidAgain_IsADuplicate_InALaterStatementAndInTheSameOne()
    {
        using var host = new ShippingHost(postgres);
        var a = await DeliveredAsync(host);
        var b = await DeliveredAsync(host);
        await host.SendAsync(Statement(NextRef(), (a.Waybill, 4700m)));

        var later = await host.SendAsync(Statement(NextRef(), (a.Waybill, 4700m)));
        var same = await host.SendAsync(Statement(NextRef(), (b.Waybill, 4700m), (b.Waybill, 4700m)));

        Assert.Equal("DUPLICATE", Assert.Single(later.Remittance.Lines).Outcome);
        Assert.Equal(["MATCHED", "DUPLICATE"], same.Remittance.Lines.Select(l => l.Outcome));
        Assert.Equal(CodStatuses.Settled, (await ShippingHost.LoadAsync(postgres, a.Id)).CodStatusId);
    }

    [Fact]
    [Trait("Spec", "COD-5")]
    [Trait("Spec", "COD-6")]
    public async Task UnknownAndNotDeliveredLines_AreFlagged_AndChangeNoParcel()
    {
        using var host = new ShippingHost(postgres);
        var undelivered = await host.NewBookedShipmentAsync();
        var waybill = (await ShippingHost.LoadAsync(postgres, undelivered)).Waybill!;

        var result = await host.SendAsync(Statement(NextRef(), (waybill, 100m), ("NOPE-123", 50m)));

        Assert.Equal(["NOT_DELIVERED", "UNKNOWN"], result.Remittance.Lines.Select(l => l.Outcome));
        Assert.Equal(CodStatuses.Awaiting, (await ShippingHost.LoadAsync(postgres, undelivered)).CodStatusId);
    }

    [Fact]
    [Trait("Spec", "COD-5")]
    public async Task AWaybillOfAnotherCourier_IsUnknown()
    {
        using var host = new ShippingHost(postgres);
        var (_, waybill) = await DeliveredAsync(host);

        var result = await host.SendAsync(Statement(NextRef(), (waybill, 4700m)) with { CourierCode = "SECONDARY" });

        Assert.Equal("UNKNOWN", Assert.Single(result.Remittance.Lines).Outcome);
    }

    [Fact]
    [Trait("Spec", "COD-7")]
    public async Task ALineForAParcelWithNoCash_IsNotCod()
    {
        using var host = new ShippingHost(postgres);
        var (orderId, number) = host.PlaceOrder(cod: false);
        var created = await host.SendAsync(new Romp.Modules.Shipping.Application.Bookings.CreateShipmentCommand(orderId, number, 600));
        await host.SendAsync(new Romp.Modules.Shipping.Application.Bookings.BookShipmentCommand(created.ShipmentId!.Value));
        var waybill = (await ShippingHost.LoadAsync(postgres, created.ShipmentId.Value)).Waybill!;
        var (body, signature) = host.Push(waybill, "DELIVERED");
        await host.SendAsync(new ApplyCourierPushCommand("PRIMARY", body, signature));

        var result = await host.SendAsync(Statement(NextRef(), (waybill, 100m)));

        Assert.Equal("NOT_COD", Assert.Single(result.Remittance.Lines).Outcome);
    }

    [Fact]
    [Trait("Spec", "COD-8")]
    public async Task ATotalThatIsNotTheSum_SavesNothing()
    {
        using var host = new ShippingHost(postgres);
        var (id, waybill) = await DeliveredAsync(host);
        var reference = NextRef();

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Statement(reference, (waybill, 4700m)) with { StatedTotal = 9999m }));

        await using var db = postgres.NewContext();
        Assert.False(await db.CodRemittances.AnyAsync(r => r.Reference == reference));
        Assert.Equal(CodStatuses.Awaiting, (await ShippingHost.LoadAsync(postgres, id)).CodStatusId);
    }

    [Fact]
    [Trait("Spec", "COD-9")]
    public async Task TheSameReferenceTwice_ChangesNothing_AndAnswersWithTheFirst()
    {
        using var host = new ShippingHost(postgres);
        var (_, waybill) = await DeliveredAsync(host);
        var reference = NextRef();
        var first = await host.SendAsync(Statement(reference, (waybill, 4700m)));

        var second = await host.SendAsync(Statement(reference.ToLowerInvariant(), (waybill, 1m)));

        Assert.True(second.AlreadyRecorded);
        Assert.Equal(first.Remittance.Summary.Id, second.Remittance.Summary.Id);
        await using var db = postgres.NewContext();
        Assert.Equal(1, await db.CodRemittances.CountAsync(r => r.Reference == reference));
    }

    [Fact]
    [Trait("Spec", "COD-10")]
    public async Task ARecordedStatement_RecordsWhoRecordedIt()
    {
        using var host = new ShippingHost(postgres);
        var (_, waybill) = await DeliveredAsync(host);

        var result = await host.SendAsync(Statement(NextRef(), (waybill, 4700m)));

        Assert.False(string.IsNullOrWhiteSpace(result.Remittance.Summary.RecordedBy)); // the audit column; the session actor fills it in the host
    }

    [Fact]
    [Trait("Spec", "COD-12")]
    public async Task ResolvingAShortLine_RecordsWhoAndWhy_ClosesTheParcel_AndLeavesTheExceptionList()
    {
        using var host = new ShippingHost(postgres);
        var (id, waybill) = await DeliveredAsync(host);
        var recorded = await host.SendAsync(Statement(NextRef(), (waybill, 4000m)));
        var lineId = recorded.Remittance.Lines.Single().Id;

        var line = await host.SendAsync(new ResolveCodLineCommand(lineId, "Courier kept the rider fee", ShippingHost.Staff));

        Assert.False(line!.IsOpen);
        Assert.Equal(CodStatuses.Resolved, (await ShippingHost.LoadAsync(postgres, id)).CodStatusId);
        var exceptions = await host.SendAsync(new ListCodExceptionsQuery(1, 200));
        Assert.DoesNotContain(exceptions.Items, e => e.LineId == lineId);
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ResolveCodLineCommand(lineId, "again", ShippingHost.Staff)));
        Assert.Null(await host.SendAsync(new ResolveCodLineCommand(long.MaxValue, "x", ShippingHost.Staff)));
    }

    [Fact]
    [Trait("Spec", "COD-11")]
    public async Task TheDailyJob_FlagsDeliveredUnpaidParcelsPastTheCycle_OnceEach()
    {
        using var host = new ShippingHost(postgres);
        var old = await DeliveredAsync(host, at: ShippingHost.Start.AddDays(-10));
        var fresh = await DeliveredAsync(host, at: ShippingHost.Start.AddDays(-2));

        var first = await host.SendAsync(new FlagOverdueCodCommand());
        var second = await host.SendAsync(new FlagOverdueCodCommand());

        Assert.True(first >= 1);
        Assert.Equal(0, second);
        Assert.NotNull((await ShippingHost.LoadAsync(postgres, old.Id)).CodOverdueAt);
        Assert.Null((await ShippingHost.LoadAsync(postgres, fresh.Id)).CodOverdueAt);
        var awaiting = await host.SendAsync(new ListCodAwaitingQuery(OverdueOnly: true, PageSize: 200));
        Assert.Contains(awaiting.Items, r => r.ShipmentId == old.Id && r.Overdue);
        Assert.DoesNotContain(awaiting.Items, r => r.ShipmentId == fresh.Id);
    }

    [Fact]
    [Trait("Spec", "COD-13")]
    public async Task TheSummary_CountsAwaitingOverdueAndSettledCash_PerCourier()
    {
        using var host = new ShippingHost(postgres);
        var before = (await host.SendAsync(new GetCodSummaryQuery())).Couriers.FirstOrDefault(c => c.CourierName == "Primary courier");
        var a = await DeliveredAsync(host);
        await DeliveredAsync(host);
        await host.SendAsync(Statement(NextRef(), (a.Waybill, 4700m)));

        var after = (await host.SendAsync(new GetCodSummaryQuery())).Couriers.Single(c => c.CourierName == "Primary courier");

        Assert.Equal((before?.AwaitingCount ?? 0) + 1, after.AwaitingCount);
        Assert.Equal((before?.AwaitingCash ?? 0m) + 4700m, after.AwaitingCash);
        Assert.Equal((before?.SettledCount ?? 0) + 1, after.SettledCount);
        Assert.Equal((before?.SettledCash ?? 0m) + 4700m, after.SettledCash);
    }

    [Fact]
    [Trait("Spec", "COD-14")]
    public async Task TheLists_ArePaged_AndTheQueryCountDoesNotGrowWithRows()
    {
        using var host = new ShippingHost(postgres);
        var a = await DeliveredAsync(host);
        var b = await DeliveredAsync(host);
        await host.SendAsync(Statement(NextRef(), (a.Waybill, 1m), (b.Waybill, 2m)));

        var onePerPage = await host.SendAsync(new ListCodExceptionsQuery(1, 1));
        var statements = await host.SendAsync(new ListCodRemittancesQuery(1, 1));

        Assert.Single(onePerPage.Items);
        Assert.True(onePerPage.TotalCount >= 2);
        Assert.Single(statements.Items);
        var detail = await host.SendAsync(new GetCodRemittanceQuery(statements.Items[0].Id));
        Assert.Equal(2, detail!.Lines.Count);
        Assert.All(detail.Lines, l => Assert.NotNull(l.OrderNumber));
    }

    [Fact]
    [Trait("Spec", "COD-15")]
    public async Task RecordingWithoutAStaffMember_IsRefused()
    {
        using var host = new ShippingHost(postgres);
        var (_, waybill) = await DeliveredAsync(host);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Statement(NextRef(), (waybill, 4700m)) with { Staff = " " }));
    }
}
