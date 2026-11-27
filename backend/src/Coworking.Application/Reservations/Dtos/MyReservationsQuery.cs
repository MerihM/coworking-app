using Coworking.Application.Common.Pagination;

namespace Coworking.Application.Reservations.Dtos;

/// <summary>Which part of the member's reservations to list (FR06: "active and previous").</summary>
public enum ReservationScope
{
    /// <summary>Confirmed and not yet ended, soonest first.</summary>
    Upcoming,

    /// <summary>Ended (Completed) or cancelled, newest first.</summary>
    Past,
}

/// <summary>Query string of <c>GET /api/reservations/mine</c>, e.g. <c>?scope=Past&amp;page=2</c>.</summary>
public sealed class MyReservationsQuery : PageQuery
{
    /// <summary>Upcoming (default) or Past.</summary>
    public ReservationScope Scope { get; set; } = ReservationScope.Upcoming;
}
