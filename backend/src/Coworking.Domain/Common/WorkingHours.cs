using System.Globalization;

namespace Coworking.Domain.Common;

/// <summary>
/// Business rule BR04: a reservation must be within the center's working hours
/// (weekdays 07:00–22:00, Saturday 09:00–17:00, closed on Sunday) and last at least 1 hour.
/// Evaluated in Sarajevo local time.
/// </summary>
public static class WorkingHours
{
    /// <summary>Shortest allowed reservation (BR04).</summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromHours(1);

    /// <summary>Opening and closing time for a day of the week, or null when the center is closed.</summary>
    /// <param name="day">Day of the week (local).</param>
    public static (TimeOnly Open, TimeOnly Close)? GetHours(DayOfWeek day) =>
        day switch
        {
            DayOfWeek.Sunday => null,
            DayOfWeek.Saturday => (new TimeOnly(9, 0), new TimeOnly(17, 0)),
            _ => (new TimeOnly(7, 0), new TimeOnly(22, 0)),
        };

    /// <summary>
    /// Throws a <see cref="DomainException"/> with rule code BR04 when the period is not allowed.
    /// Corresponds to "provjeriRadnoVrijeme(pocetak, kraj)" in the Sequence diagram.
    /// </summary>
    /// <param name="startsAt">Start of the reservation (any offset, compared as an instant).</param>
    /// <param name="endsAt">End of the reservation.</param>
    public static void EnsureWithin(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (endsAt <= startsAt)
        {
            throw new DomainException("The reservation must end after it starts.", "BR04");
        }

        if (endsAt - startsAt < MinimumDuration)
        {
            throw new DomainException("A reservation must last at least 1 hour.", "BR04");
        }

        var localStart = CenterTimeZone.ToLocal(startsAt);
        var localEnd = CenterTimeZone.ToLocal(endsAt);

        if (localStart.Date != localEnd.Date)
        {
            throw new DomainException("A reservation must start and end on the same day.", "BR04");
        }

        var hours =
            GetHours(localStart.DayOfWeek)
            ?? throw new DomainException("The center is closed on Sundays.", "BR04");

        if (
            TimeOnly.FromDateTime(localStart) < hours.Open
            || TimeOnly.FromDateTime(localEnd) > hours.Close
        )
        {
            throw new DomainException(
                $"On {localStart.DayOfWeek}s the center is open from "
                    + $"{hours.Open.ToString("HH:mm", CultureInfo.InvariantCulture)} to "
                    + $"{hours.Close.ToString("HH:mm", CultureInfo.InvariantCulture)}.",
                "BR04"
            );
        }
    }
}
