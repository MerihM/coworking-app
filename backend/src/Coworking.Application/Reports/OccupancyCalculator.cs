using Coworking.Domain.Common;

namespace Coworking.Application.Reports;

/// <summary>
/// The arithmetic of the FR11 report, kept pure (no database, no clock) so it can be unit-tested
/// on its own: period bounds, opening hours of a period, overlap of a reservation with a period,
/// and the occupancy rate.
/// </summary>
/// <remarks>
/// Hours are <see cref="decimal"/> computed from ticks, so 2 h is exactly 2 and the sums in the
/// report add up exactly (no floating-point leftovers like 1.9999999).
/// </remarks>
public static class OccupancyCalculator
{
    /// <summary>
    /// Turns the inclusive local days <paramref name="from"/>–<paramref name="to"/> into a half-open
    /// UTC period: <c>from 00:00</c> (Sarajevo) up to, but not including, <c>to + 1 day 00:00</c>
    /// (ADR 0002). Daylight saving time is handled by <see cref="CenterTimeZone.FromLocal"/>.
    /// </summary>
    /// <param name="from">First local day of the report.</param>
    /// <param name="to">Last local day of the report (inclusive).</param>
    /// <returns>The period start and end as UTC instants.</returns>
    public static (DateTimeOffset Start, DateTimeOffset End) PeriodBounds(DateOnly from, DateOnly to) =>
        (CenterTimeZone.FromLocal(from, TimeOnly.MinValue), CenterTimeZone.FromLocal(to.AddDays(1), TimeOnly.MinValue));

    /// <summary>
    /// How many hours ONE workspace can be reserved in the period: the opening hours of every day
    /// (weekday 07–22 = 15 h, Saturday 09–17 = 8 h, Sunday closed = 0 h). Uses
    /// <see cref="WorkingHours.GetHours"/>, the same definition as BR04.
    /// </summary>
    /// <param name="from">First local day (inclusive).</param>
    /// <param name="to">Last local day (inclusive).</param>
    /// <returns>Opening hours of the period; 0 when <paramref name="to"/> is before <paramref name="from"/>.</returns>
    public static decimal OpenHours(DateOnly from, DateOnly to)
    {
        var total = 0m;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (WorkingHours.GetHours(day.DayOfWeek) is { } hours)
            {
                total += ToHours(hours.Close - hours.Open);
            }
        }

        return total;
    }

    /// <summary>
    /// The part of a reservation that lies inside the period, in hours (0 when they don't overlap).
    /// Half-open on both sides, like BR01: a reservation ending exactly at the period start adds 0.
    /// </summary>
    /// <param name="startsAt">Reservation start.</param>
    /// <param name="endsAt">Reservation end.</param>
    /// <param name="periodStart">Period start.</param>
    /// <param name="periodEnd">Period end (exclusive).</param>
    /// <returns>Overlapping hours.</returns>
    public static decimal OverlapHours(
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd
    )
    {
        var start = startsAt > periodStart ? startsAt : periodStart;
        var end = endsAt < periodEnd ? endsAt : periodEnd;
        return end > start ? ToHours(end - start) : 0m;
    }

    /// <summary>Reserved ÷ available, rounded to 4 decimals (0.1333 = 13.33 %); 0 when nothing was available.</summary>
    /// <param name="reservedHours">Reserved hours.</param>
    /// <param name="availableHours">Available (opening) hours.</param>
    /// <returns>The occupancy rate between 0 and 1.</returns>
    public static decimal Rate(decimal reservedHours, decimal availableHours) =>
        availableHours <= 0 ? 0m : Math.Round(reservedHours / availableHours, 4, MidpointRounding.AwayFromZero);

    /// <summary>A duration in hours, exactly (from ticks, not from a double).</summary>
    /// <param name="duration">The duration.</param>
    /// <returns>Hours as a decimal.</returns>
    public static decimal ToHours(TimeSpan duration) => (decimal)duration.Ticks / TimeSpan.TicksPerHour;
}
