using Coworking.Domain.Entities;
using Coworking.Domain.Enums;

namespace Coworking.Application.Availability;

/// <summary>
/// Reusable query building blocks for "which workspaces can be booked?". FR02 uses them now;
/// Sprint 5 reuses them for the BR01 service check and the UC06 "offer another workspace" step,
/// so the definition of "free" exists in exactly one place.
/// </summary>
public static class WorkspaceAvailabilityQueries
{
    /// <summary>
    /// Workspaces that are offered at all: the workspace AND its room are active, and it is not a
    /// fixed desk (those are assigned to one member, not booked by the hour – Sprint 3 decision).
    /// </summary>
    /// <param name="workspaces">Any workspace query.</param>
    public static IQueryable<Workspace> Offered(this IQueryable<Workspace> workspaces) =>
        workspaces.Where(w => w.IsActive && w.Room.IsActive && w.Type != WorkspaceType.FixedDesk);

    /// <summary>
    /// Workspaces with NO Confirmed reservation overlapping [<paramref name="startsAt"/>,
    /// <paramref name="endsAt"/>) – the same half-open rule as Reservation.OverlapsWith and the
    /// EXCLUDE constraint ex_reservations_no_overlap (BR01). Touching periods (10–12 and 12–14)
    /// don't overlap (TC04).
    /// </summary>
    /// <param name="workspaces">Any workspace query.</param>
    /// <param name="reservations">The reservations table (Workspace has no reservation collection).</param>
    /// <param name="startsAt">Start (UTC).</param>
    /// <param name="endsAt">End (UTC).</param>
    /// <remarks>
    /// EF turns this into <c>WHERE NOT EXISTS (SELECT 1 FROM reservations r WHERE r.workspace_id =
    /// w.id AND r.status = 'Confirmed' AND r.starts_at &lt; @end AND r.ends_at &gt; @start)</c>.
    /// NOT EXISTS stops at the first matching row, and the GiST index behind the EXCLUDE
    /// constraint (workspace_id, time range) lets PostgreSQL find that row without a full scan.
    /// </remarks>
    public static IQueryable<Workspace> FreeBetween(
        this IQueryable<Workspace> workspaces,
        IQueryable<Reservation> reservations,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt
    ) =>
        workspaces.Where(w =>
            !reservations.Any(r =>
                r.WorkspaceId == w.Id
                && r.Status == ReservationStatus.Confirmed
                && r.StartsAt < endsAt
                && r.EndsAt > startsAt
            )
        );
}
