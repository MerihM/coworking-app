using Coworking.Application.Common.Pagination;
using Coworking.Domain.Enums;

namespace Coworking.Application.Reservations.Dtos;

/// <summary>
/// Query string of <c>GET /api/reservations</c> (reception), e.g. <c>?date=2026-12-01&amp;status=Confirmed</c>
/// for "who is coming today". Sorted by start time, soonest first.
/// </summary>
public sealed class ListReservationsQuery : PageQuery
{
    /// <summary>Only reservations that start on this local Sarajevo date.</summary>
    public DateOnly? Date { get; set; }

    /// <summary>Confirmed (not ended yet), Cancelled or Completed (ended) – the derived status.</summary>
    public ReservationStatus? Status { get; set; }

    /// <summary>Part of the member's name or e-mail, or of the workspace code (case-insensitive).</summary>
    public string? Search { get; set; }

    /// <summary>Only this member's reservations (from the member detail page).</summary>
    public int? MemberId { get; set; }
}
