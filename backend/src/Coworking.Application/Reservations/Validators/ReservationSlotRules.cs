using Coworking.Application.Availability.Validators;
using Coworking.Application.Common.Localization;
using Coworking.Domain.Common;

namespace Coworking.Application.Reservations.Validators;

/// <summary>
/// Shape rules shared by the reservation request and the alternatives query. The limits are the
/// availability search's, so every slot the search shows can also be booked.
/// </summary>
internal static class ReservationSlotRules
{
    /// <summary>How far ahead one can reserve – the same 90 days the search allows.</summary>
    public const int MaxDaysAhead = AvailabilityQueryValidator.MaxDaysAhead;

    /// <summary>Is the date within <see cref="MaxDaysAhead"/> days from today (Sarajevo)?</summary>
    /// <param name="date">Local date.</param>
    /// <param name="time">The clock.</param>
    public static bool WithinBookingWindow(DateOnly date, TimeProvider time) =>
        date <= CenterTimeZone.ToLocalDate(time.GetUtcNow()).AddDays(MaxDaysAhead);

    /// <summary>A time on a quarter hour (09:00, 09:15, ...) – the slots the UI offers.</summary>
    /// <param name="time">The time; null passes (NotNull reports that).</param>
    public static bool OnQuarterHour(TimeOnly? time) =>
        time is not { } t
        || (t.Minute % AvailabilityQueryValidator.MinuteStep == 0 && t.Second == 0 && t.Millisecond == 0);

    /// <summary>Translated "too far ahead" message.</summary>
    public static string TooFarAheadMessage() =>
        Localized.Text(
            $"You can reserve at most {MaxDaysAhead} days ahead.",
            $"Možete rezervisati najviše {MaxDaysAhead} dana unaprijed."
        );

    /// <summary>Translated "quarter hour" message.</summary>
    public static string QuarterHourMessage() =>
        Localized.Text(
            "Times must be on a quarter hour (e.g. 09:00, 09:15).",
            "Vrijeme mora biti na četvrt sata (npr. 09:00, 09:15)."
        );

    /// <summary>Translated "end after start" message.</summary>
    public static string EndAfterStartMessage() =>
        Localized.Text("The end time must be after the start time.", "Vrijeme završetka mora biti nakon početka.");
}
