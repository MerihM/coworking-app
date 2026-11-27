namespace Coworking.Application.Reservations.Dtos;

/// <summary>
/// Body of <c>POST /api/reservations/mine</c> (UC04 step 3: the member picks a workspace, a date and
/// the start and end time). Date and times are LOCAL Sarajevo time, exactly like the availability
/// search the member came from; the service converts them to UTC instants (ADR 0002).
/// </summary>
/// <param name="WorkspaceId">The workspace from the availability results.</param>
/// <param name="Date">Local date, "yyyy-MM-dd"; at most 90 days ahead.</param>
/// <param name="From">Local start, "HH:mm:ss" (a quarter hour, e.g. "10:00:00").</param>
/// <param name="To">Local end, after <paramref name="From"/>, on a quarter hour.</param>
public sealed record CreateReservationRequest(int WorkspaceId, DateOnly Date, TimeOnly From, TimeOnly To);
