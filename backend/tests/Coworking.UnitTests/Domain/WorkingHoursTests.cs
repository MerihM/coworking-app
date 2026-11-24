using System.Globalization;
using Coworking.Domain.Common;

namespace Coworking.UnitTests.Domain;

/// <summary>BR04: working hours and minimum duration, in Sarajevo local time.</summary>
public class WorkingHoursTests
{
    [Theory]
    [InlineData("2026-11-23", "10:00", "12:00")] // Monday, the TC01 slot
    [InlineData("2026-11-23", "07:00", "08:00")] // exactly at opening
    [InlineData("2026-11-23", "21:00", "22:00")] // ends exactly at closing
    [InlineData("2026-11-28", "09:00", "17:00")] // Saturday, the whole working day
    [InlineData("2026-07-15", "07:00", "08:00")] // summer time (UTC+2) converted correctly
    public void EnsureWithin_AllowedSlot_DoesNotThrow(string date, string from, string to)
    {
        var (start, end) = Slot(date, from, to);

        var exception = Record.Exception(() => WorkingHours.EnsureWithin(start, end));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("2026-11-28", "16:00", "18:00")] // TC05: Saturday closes at 17:00
    [InlineData("2026-11-23", "06:00", "08:00")] // starts before opening
    [InlineData("2026-11-23", "21:00", "23:00")] // ends after closing
    [InlineData("2026-11-29", "10:00", "12:00")] // Sunday: closed
    [InlineData("2026-11-23", "10:00", "10:30")] // shorter than 1 hour
    [InlineData("2026-11-23", "12:00", "10:00")] // ends before it starts
    public void EnsureWithin_ForbiddenSlot_ThrowsBr04(string date, string from, string to)
    {
        var (start, end) = Slot(date, from, to);

        var exception = Assert.Throws<DomainException>(() => WorkingHours.EnsureWithin(start, end));

        Assert.Equal("BR04", exception.RuleCode);
    }

    private static (DateTimeOffset Start, DateTimeOffset End) Slot(
        string date,
        string from,
        string to
    )
    {
        var day = DateOnly.Parse(date, CultureInfo.InvariantCulture);
        return (
            CenterTimeZone.FromLocal(day, TimeOnly.Parse(from, CultureInfo.InvariantCulture)),
            CenterTimeZone.FromLocal(day, TimeOnly.Parse(to, CultureInfo.InvariantCulture))
        );
    }
}
