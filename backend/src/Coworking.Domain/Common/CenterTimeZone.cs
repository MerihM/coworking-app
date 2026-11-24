namespace Coworking.Domain.Common;

/// <summary>
/// Converts between UTC (how all instants are stored) and the local time of the coworking
/// center in Sarajevo (how business rules are defined). Handles summer time automatically:
/// UTC+1 in winter, UTC+2 in summer.
/// </summary>
public static class CenterTimeZone
{
    /// <summary>
    /// The center's time zone (IANA id "Europe/Sarajevo"). On Linux .NET reads it from
    /// /usr/share/zoneinfo (package tzdata), which is why the API's Docker image will need tzdata.
    /// </summary>
    public static TimeZoneInfo TimeZone { get; } =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    /// <summary>Wall-clock time in Sarajevo for a given instant, e.g. 09:00 UTC → 10:00 (winter).</summary>
    /// <param name="instant">Any instant; its offset does not matter.</param>
    public static DateTime ToLocal(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime;

    /// <summary>The calendar date in Sarajevo for a given instant (used for BR02 and BR05).</summary>
    /// <param name="instant">Any instant.</param>
    public static DateOnly ToLocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(ToLocal(instant));

    /// <summary>
    /// Builds a UTC instant from a Sarajevo date and wall-clock time,
    /// e.g. 2026-07-15 07:00 (summer) → 2026-07-15 05:00 UTC.
    /// </summary>
    /// <param name="date">Local date.</param>
    /// <param name="time">Local wall-clock time.</param>
    public static DateTimeOffset FromLocal(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time); // Kind = Unspecified: "a clock reading"
        var offset = TimeZone.GetUtcOffset(local); // +01:00 or +02:00 depending on the date
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
