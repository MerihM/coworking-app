using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;

namespace Coworking.Application.Reservations.Dtos;

/// <summary>A reservation as the member (FR06) and the reception see it.</summary>
/// <param name="Id">Reservation id.</param>
/// <param name="MemberId">Member id.</param>
/// <param name="MemberName">"First Last".</param>
/// <param name="MemberEmail">Member's e-mail (reception searches by it).</param>
/// <param name="WorkspaceId">Workspace id.</param>
/// <param name="WorkspaceCode">Workspace label, e.g. "OS-01".</param>
/// <param name="WorkspaceType">HotDesk, Office or MeetingRoom.</param>
/// <param name="Capacity">Number of people the workspace fits.</param>
/// <param name="RoomName">Room the workspace is in.</param>
/// <param name="StartsAt">Start (UTC).</param>
/// <param name="EndsAt">End (UTC, exclusive).</param>
/// <param name="Date">Local Sarajevo date – what the UI shows.</param>
/// <param name="From">Local start time.</param>
/// <param name="To">Local end time.</param>
/// <param name="Status">Confirmed, Cancelled or Completed (derived: Confirmed + ended = Completed).</param>
/// <param name="CreatedAt">When it was made (UTC).</param>
/// <param name="CancelledAt">When it was cancelled (UTC), or null.</param>
/// <param name="CancelledByName">Employee who cancelled it ("First Last"); null = the member did, or not cancelled.</param>
/// <param name="CanMemberCancel">BR03: can the member still cancel it themselves (at least 2 h before the start)?</param>
public sealed record ReservationResponse(
    int Id,
    int MemberId,
    string MemberName,
    string MemberEmail,
    int WorkspaceId,
    string WorkspaceCode,
    WorkspaceType WorkspaceType,
    int Capacity,
    string RoomName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateOnly Date,
    TimeOnly From,
    TimeOnly To,
    ReservationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    string? CancelledByName,
    bool CanMemberCancel
)
{
    /// <summary>
    /// Maps a reservation loaded with its member, workspace + room and the cancelling employee.
    /// In memory (not a SQL projection) so the domain rules EffectiveStatusAt and
    /// CanBeCancelledByMember are reused instead of being written a second time in LINQ.
    /// </summary>
    /// <param name="reservation">The reservation.</param>
    /// <param name="now">Current moment (status and BR03 depend on it).</param>
    /// <returns>The response.</returns>
    public static ReservationResponse Map(Reservation reservation, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var localStart = CenterTimeZone.ToLocal(reservation.StartsAt);
        var localEnd = CenterTimeZone.ToLocal(reservation.EndsAt);

        return new ReservationResponse(
            reservation.Id,
            reservation.MemberId,
            $"{reservation.Member.FirstName} {reservation.Member.LastName}",
            reservation.Member.Email,
            reservation.WorkspaceId,
            reservation.Workspace.Code,
            reservation.Workspace.Type,
            reservation.Workspace.Capacity,
            reservation.Workspace.Room.Name,
            reservation.StartsAt,
            reservation.EndsAt,
            DateOnly.FromDateTime(localStart),
            TimeOnly.FromDateTime(localStart),
            TimeOnly.FromDateTime(localEnd),
            reservation.EffectiveStatusAt(now),
            reservation.CreatedAt,
            reservation.CancelledAt,
            reservation.CancelledByEmployee is { } employee ? $"{employee.FirstName} {employee.LastName}" : null,
            reservation.CanBeCancelledByMember(now)
        );
    }
}
