namespace Coworking.Application.Reservations.Dtos;

/// <summary>
/// Query string of <c>GET /api/reservations/alternatives</c> (UC06): the slot the member wanted
/// but got a 409 for, e.g. <c>?workspaceId=7&amp;date=2026-12-01&amp;from=11:00&amp;to=13:00</c>.
/// Local Sarajevo date and times, like the availability search.
/// </summary>
/// <remarks>
/// Nullable on purpose (same as AvailabilityQuery): a missing parameter arrives as null and the
/// validator answers with a clear "required" message instead of the binder using 0 / 00:00.
/// </remarks>
public sealed class ReservationAlternativesQuery
{
    /// <summary>The workspace that was taken. Required.</summary>
    public int? WorkspaceId { get; set; }

    /// <summary>Local date. Required.</summary>
    public DateOnly? Date { get; set; }

    /// <summary>Local start time, quarter hour. Required.</summary>
    public TimeOnly? From { get; set; }

    /// <summary>Local end time, quarter hour, after <see cref="From"/>. Required.</summary>
    public TimeOnly? To { get; set; }
}
