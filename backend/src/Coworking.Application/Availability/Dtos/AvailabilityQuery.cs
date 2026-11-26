using Coworking.Domain.Enums;

namespace Coworking.Application.Availability.Dtos;

/// <summary>
/// Query string of <c>GET /api/availability</c>, e.g.
/// <c>?date=2026-12-01&amp;from=09:00&amp;to=12:00&amp;type=Office&amp;minCapacity=4</c>.
/// Date and times are LOCAL Sarajevo time (what a person types), not UTC – the service converts.
/// </summary>
/// <remarks>
/// The three required values are nullable on purpose: a missing parameter then arrives as null and
/// the validator answers with a clear, translated "required" message, instead of the model binder
/// quietly using 0001-01-01 / 00:00.
/// </remarks>
public sealed class AvailabilityQuery
{
    /// <summary>Local date, format yyyy-MM-dd. Required; at most 90 days ahead.</summary>
    public DateOnly? Date { get; set; }

    /// <summary>Local start time, format HH:mm, in 15-minute steps. Required.</summary>
    public TimeOnly? From { get; set; }

    /// <summary>Local end time, format HH:mm, in 15-minute steps, after <see cref="From"/>. Required.</summary>
    public TimeOnly? To { get; set; }

    /// <summary>Only this type: HotDesk, Office or MeetingRoom. FixedDesk is not searchable (assigned, not booked).</summary>
    public WorkspaceType? Type { get; set; }

    /// <summary>Only workspaces for at least this many people (1–50), e.g. a meeting room for 6.</summary>
    public int? MinCapacity { get; set; }
}
