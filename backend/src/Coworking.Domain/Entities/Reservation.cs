using Coworking.Domain.Common;
using Coworking.Domain.Enums;

namespace Coworking.Domain.Entities;

/// <summary>
/// A member's reservation of a workspace for a period (design document: Rezervacija).
/// The period is half-open [StartsAt, EndsAt): a reservation ending at 12:00 and one starting
/// at 12:00 do not overlap (TC04).
/// </summary>
public class Reservation : Entity
{
    /// <summary>A member may cancel at the latest this long before the start (BR03).</summary>
    public static readonly TimeSpan MemberCancellationDeadline = TimeSpan.FromHours(2);

    /// <summary>Used by EF Core when loading from the database.</summary>
    private Reservation() { }

    private Reservation(
        Member member,
        Workspace workspace,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset createdAt
    )
    {
        Member = member;
        Workspace = workspace;
        StartsAt = startsAt.ToUniversalTime();
        EndsAt = endsAt.ToUniversalTime();
        CreatedAt = createdAt.ToUniversalTime();
        Status = ReservationStatus.Confirmed;
    }

    /// <summary>Foreign key of the member.</summary>
    public int MemberId { get; private set; }

    /// <summary>The member who reserved.</summary>
    public Member Member { get; private set; } = null!;

    /// <summary>Foreign key of the workspace.</summary>
    public int WorkspaceId { get; private set; }

    /// <summary>The reserved workspace.</summary>
    public Workspace Workspace { get; private set; } = null!;

    /// <summary>Start in UTC, inclusive (pocetak).</summary>
    public DateTimeOffset StartsAt { get; private set; }

    /// <summary>End in UTC, exclusive (kraj).</summary>
    public DateTimeOffset EndsAt { get; private set; }

    /// <summary>When the reservation was made, UTC (datumKreiranja).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Confirmed, Cancelled or Completed (status).</summary>
    public ReservationStatus Status { get; private set; }

    /// <summary>When it was cancelled, if it was. Not in the Class diagram; added for the change history (chapter 1.4).</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Foreign key of the employee who cancelled it (null = the member cancelled, or not cancelled).</summary>
    public int? CancelledByEmployeeId { get; private set; }

    /// <summary>The employee who cancelled it on behalf of the member (FR05, UC07).</summary>
    public Employee? CancelledByEmployee { get; private set; }

    /// <summary>
    /// Creates a confirmed reservation (UC04, steps 3–6; diagram: «create» + potvrdi()).
    /// Checks, in the order of the Activity diagram: working hours (BR04), then active membership
    /// on the reservation date (BR02). Availability (BR01) is checked by the reservation service
    /// before calling this, and guaranteed by the database constraint.
    /// </summary>
    /// <param name="member">The member; memberships must be loaded.</param>
    /// <param name="workspace">The workspace.</param>
    /// <param name="startsAt">Start.</param>
    /// <param name="endsAt">End.</param>
    /// <param name="now">Current moment.</param>
    /// <returns>A reservation with status Confirmed.</returns>
    public static Reservation Create(
        Member member,
        Workspace workspace,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(workspace);

        member.EnsureActive();

        if (!workspace.IsActive)
        {
            throw new DomainException(
                $"Workspace {workspace.Code} is not available for reservations."
            );
        }

        if (startsAt <= now)
        {
            throw new DomainException("A reservation must start in the future.");
        }

        WorkingHours.EnsureWithin(startsAt, endsAt); // BR04 (UC04 flow 3a)

        var reservationDate = CenterTimeZone.ToLocalDate(startsAt);
        if (!member.HasActiveMembershipOn(reservationDate)) // BR02 (UC04 flow 4a)
        {
            throw new DomainException(
                "You need an active membership on the reservation date.",
                "BR02"
            );
        }

        return new Reservation(member, workspace, startsAt, endsAt, now);
    }

    /// <summary>
    /// BR01: does this reservation block the given period? Only confirmed reservations block;
    /// periods that only touch (12:00–12:00) do not overlap (TC04).
    /// </summary>
    /// <param name="startsAt">Start of the other period.</param>
    /// <param name="endsAt">End of the other period.</param>
    public bool OverlapsWith(DateTimeOffset startsAt, DateTimeOffset endsAt) =>
        Status == ReservationStatus.Confirmed && StartsAt < endsAt && startsAt < EndsAt;

    /// <summary>
    /// BR03: can the member still cancel it themselves? Yes until exactly 2 hours before the start
    /// (diagram: mozeSeOtkazati()).
    /// </summary>
    /// <param name="now">Current moment.</param>
    public bool CanBeCancelledByMember(DateTimeOffset now) =>
        Status == ReservationStatus.Confirmed && now <= StartsAt - MemberCancellationDeadline;

    /// <summary>
    /// The status as the user should see it at <paramref name="now"/>: a Confirmed reservation whose
    /// period has ended is shown as Completed (Iskoristena) – derived on read, like an expired
    /// membership, so no background job has to update rows (CW-29). The stored status is not changed.
    /// </summary>
    /// <param name="now">Current moment.</param>
    /// <returns>Confirmed, Cancelled or Completed.</returns>
    public ReservationStatus EffectiveStatusAt(DateTimeOffset now) =>
        Status == ReservationStatus.Confirmed && now >= EndsAt ? ReservationStatus.Completed : Status;

    /// <summary>Cancellation by the member (FR05, UC07; diagram: otkazi()). Enforces BR03.</summary>
    /// <param name="now">Current moment.</param>
    public void CancelByMember(DateTimeOffset now)
    {
        EnsureConfirmed();

        if (!CanBeCancelledByMember(now))
        {
            throw new DomainException(
                "Members can cancel a reservation at the latest 2 hours before it starts. Please contact the reception.",
                "BR03"
            );
        }

        Status = ReservationStatus.Cancelled;
        CancelledAt = now.ToUniversalTime();
    }

    /// <summary>
    /// Cancellation by an employee on behalf of the member (FR05, BR03: also after the 2-hour deadline).
    /// Not allowed once the reservation has ended.
    /// </summary>
    /// <param name="employee">The employee who cancels (stored for the change history).</param>
    /// <param name="now">Current moment.</param>
    public void CancelByEmployee(Employee employee, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(employee);
        EnsureConfirmed();

        if (now >= EndsAt)
        {
            throw new DomainException("A reservation that has already ended can't be cancelled.");
        }

        Status = ReservationStatus.Cancelled;
        CancelledAt = now.ToUniversalTime();
        CancelledByEmployee = employee;
        CancelledByEmployeeId = employee.Id;
    }

    /// <summary>Marks a confirmed reservation as used once its period has ended.</summary>
    /// <param name="now">Current moment.</param>
    public void Complete(DateTimeOffset now)
    {
        EnsureConfirmed();

        if (now < EndsAt)
        {
            throw new DomainException("A reservation can be completed only after it ends.");
        }

        Status = ReservationStatus.Completed;
    }

    private void EnsureConfirmed()
    {
        if (Status != ReservationStatus.Confirmed)
        {
            throw new DomainException(
                $"Only confirmed reservations can be changed (current status: {Status})."
            );
        }
    }
}
