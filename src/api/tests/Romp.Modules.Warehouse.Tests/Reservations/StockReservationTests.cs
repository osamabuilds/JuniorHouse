using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Domain.Reservations;

namespace Romp.Modules.Warehouse.Tests.Reservations;

public sealed class StockReservationTests
{
    private static readonly DateTimeOffset Until = new(2026, 11, 2, 12, 30, 0, TimeSpan.Zero);

    private static StockReservation Held(string key = "k-1") => StockReservation.Create(key, [(2, 3), (1, 1)], Until);

    [Fact]
    [Trait("Spec", "RSV-1")]
    public void ANewReservation_IsHeld_WithItsLinesSortedByVariant_AndAnExpiry()
    {
        var r = Held();

        Assert.Equal(ReservationStatuses.Held, r.StatusId);
        Assert.Equal(Until, r.HeldUntil);
        Assert.Equal([1L, 2L], r.Lines.Select(l => l.VariantRefId));
    }

    [Fact]
    [Trait("Spec", "RSV-4")]
    public void TheSameVariantTwice_IsOneLineWithTheSum()
    {
        var r = StockReservation.Create("k", [(5, 2), (5, 3)], Until);

        var line = Assert.Single(r.Lines);
        Assert.Equal(5, line.Quantity);
    }

    [Fact]
    [Trait("Spec", "RSV-4")]
    public void Confirming_FirmsTheHold_AndRepeatingItChangesNothing()
    {
        var r = Held();

        Assert.Equal(ReservationTransition.Changed, r.Confirm("RMP-2026-00001-1"));
        Assert.Equal(ReservationTransition.Unchanged, r.Confirm("RMP-2026-99999-9"));

        Assert.Equal(ReservationStatuses.Confirmed, r.StatusId);
        Assert.Equal("RMP-2026-00001-1", r.OrderNumber);
        Assert.Null(r.HeldUntil);
    }

    [Fact]
    [Trait("Spec", "RSV-5")]
    public void Releasing_WorksFromHeldOrConfirmed_AndOnlyOnce()
    {
        var held = Held();
        var confirmed = Held("k-2");
        confirmed.Confirm("RMP-1");

        Assert.Equal(ReservationTransition.Changed, held.Release());
        Assert.Equal(ReservationTransition.Changed, confirmed.Release());
        Assert.Equal(ReservationTransition.Unchanged, held.Release());
        Assert.Equal(ReservationStatuses.Released, held.StatusId);
        Assert.Null(held.HeldUntil);
    }

    [Fact]
    [Trait("Spec", "RSV-5")]
    public void AReleasedReservation_CannotBeConfirmed_SoALateOrderEventIsNoticed()
    {
        var r = Held();
        r.Release();

        Assert.Equal(ReservationTransition.NotAllowed, r.Confirm("RMP-1"));
        Assert.Equal(ReservationStatuses.Released, r.StatusId);
    }

    [Fact]
    [Trait("Spec", "RSV-9")]
    public void Converting_NeedsAConfirmedReservation_AndAPickedOneCannotBeReleased()
    {
        var r = Held();
        Assert.Equal(ReservationTransition.NotAllowed, r.Convert());

        r.Confirm("RMP-1");
        Assert.Equal(ReservationTransition.Changed, r.Convert());
        Assert.Equal(ReservationTransition.Unchanged, r.Convert());
        Assert.Equal(ReservationTransition.NotAllowed, r.Release());
        Assert.Equal(ReservationStatuses.Converted, r.StatusId);
    }

    [Theory]
    [Trait("Spec", "RSV-2")]
    [InlineData("")]
    [InlineData("   ")]
    public void AReservationNeedsAKey(string key)
    {
        Assert.Throws<DomainException>(() => StockReservation.Create(key, [(1, 1)], Until));
    }

    [Fact]
    [Trait("Spec", "RSV-2")]
    public void BadLines_AreRefused()
    {
        Assert.Throws<DomainException>(() => StockReservation.Create("k", [], Until));
        Assert.Throws<DomainException>(() => StockReservation.Create("k", [(1, 0)], Until));
        Assert.Throws<DomainException>(() => StockReservation.Create("k", [(0, 1)], Until));
        Assert.Throws<DomainException>(() => StockReservation.Create("k", [(1, StockReservation.MaxQuantityPerLine + 1)], Until));
        Assert.Throws<DomainException>(() => StockReservation.Create("k", Enumerable.Range(1, StockReservation.MaxLines + 1).Select(i => ((long)i, 1)), Until));
        Assert.Throws<DomainException>(() => StockReservation.Create(new string('k', StockReservation.MaxKeyLength + 1), [(1, 1)], Until));
    }
}
