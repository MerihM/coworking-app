using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using static Coworking.UnitTests.Domain.TestData;

namespace Coworking.UnitTests.Domain;

/// <summary>Reservation rules: TC01–TC06 at the domain level (BR01–BR04).</summary>
public class ReservationTests
{
    [Fact]
    public void TC01_Create_WithActiveMembershipInWorkingHours_IsConfirmed()
    {
        var member = MemberWithPaidMembership(Now);

        var reservation = Reservation.Create(
            member,
            NewDesk(),
            At(2026, 11, 23, 10),
            At(2026, 11, 23, 12),
            Now
        );

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(Now, reservation.CreatedAt);
    }

    [Fact]
    public void TC02_Create_WithExpiredMembership_ThrowsBr02()
    {
        // Paid on 1 October: 30 days → valid until 30 October, so not valid on 23 November.
        var member = MemberWithPaidMembership(At(2026, 10, 1, 9));

        var exception = Assert.Throws<DomainException>(() =>
            Reservation.Create(member, NewDesk(), At(2026, 11, 23, 10), At(2026, 11, 23, 12), Now)
        );

        Assert.Equal("BR02", exception.RuleCode);
    }

    [Fact]
    public void TC05_Create_SaturdayAfterClosing_ThrowsBr04()
    {
        var member = MemberWithPaidMembership(Now);

        var exception = Assert.Throws<DomainException>(() =>
            Reservation.Create(member, NewDesk(), At(2026, 11, 28, 16), At(2026, 11, 28, 18), Now)
        );

        Assert.Equal("BR04", exception.RuleCode);
    }

    [Fact]
    public void TC03_OverlapsWith_PartiallyOverlappingPeriod_ReturnsTrue()
    {
        var existing = ConfirmedReservation(10, 12);

        Assert.True(existing.OverlapsWith(At(2026, 11, 23, 11), At(2026, 11, 23, 13)));
    }

    [Fact]
    public void TC04_OverlapsWith_TouchingPeriod_ReturnsFalse()
    {
        var existing = ConfirmedReservation(10, 12);

        Assert.False(existing.OverlapsWith(At(2026, 11, 23, 12), At(2026, 11, 23, 14)));
    }

    [Fact]
    public void OverlapsWith_CancelledReservation_ReturnsFalse()
    {
        var existing = ConfirmedReservation(10, 12);
        existing.CancelByMember(Now);

        Assert.False(existing.OverlapsWith(At(2026, 11, 23, 10), At(2026, 11, 23, 12)));
    }

    [Fact]
    public void BR03_CancelByMember_ExactlyTwoHoursBefore_IsAllowed()
    {
        var reservation = ConfirmedReservation(10, 12);

        reservation.CancelByMember(At(2026, 11, 23, 8)); // 08:00, start 10:00

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Null(reservation.CancelledByEmployee);
    }

    [Fact]
    public void TC06_CancelByMember_OneHourBefore_ThrowsBr03()
    {
        var reservation = ConfirmedReservation(10, 12);

        var exception = Assert.Throws<DomainException>(() =>
            reservation.CancelByMember(At(2026, 11, 23, 9))
        );

        Assert.Equal("BR03", exception.RuleCode);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
    }

    [Fact]
    public void TC06_CancelByEmployee_OneHourBefore_IsAllowed()
    {
        var reservation = ConfirmedReservation(10, 12);
        var receptionist = NewReceptionist();

        reservation.CancelByEmployee(receptionist, At(2026, 11, 23, 9));

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Same(receptionist, reservation.CancelledByEmployee);
    }

    [Fact]
    public void Cancel_AlreadyCancelledReservation_Throws()
    {
        var reservation = ConfirmedReservation(10, 12);
        reservation.CancelByMember(Now);

        Assert.Throws<DomainException>(() => reservation.CancelByEmployee(NewReceptionist(), Now));
    }

    [Fact]
    public void EffectiveStatusAt_ConfirmedReservation_IsCompletedFromItsEnd()
    {
        var reservation = ConfirmedReservation(10, 12);

        Assert.Equal(ReservationStatus.Confirmed, reservation.EffectiveStatusAt(At(2026, 11, 23, 11, 59)));
        Assert.Equal(ReservationStatus.Completed, reservation.EffectiveStatusAt(At(2026, 11, 23, 12))); // end is exclusive
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status); // stored status untouched
    }

    [Fact]
    public void EffectiveStatusAt_CancelledReservation_StaysCancelledAfterItsEnd()
    {
        var reservation = ConfirmedReservation(10, 12);
        reservation.CancelByMember(Now);

        Assert.Equal(ReservationStatus.Cancelled, reservation.EffectiveStatusAt(At(2026, 11, 23, 13)));
    }

    /// <summary>A confirmed reservation on Monday 2026-11-23 between the given hours.</summary>
    private static Reservation ConfirmedReservation(int fromHour, int toHour) =>
        Reservation.Create(
            MemberWithPaidMembership(Now),
            NewDesk(),
            At(2026, 11, 23, fromHour),
            At(2026, 11, 23, toHour),
            Now
        );
}
