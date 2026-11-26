using Coworking.Domain.Common;

namespace Coworking.UnitTests.Domain;

/// <summary>
/// The local → UTC conversion the availability search relies on (ADR 0002, CW-21), around the
/// switch to summer time. In 2027 Sarajevo moves from UTC+1 to UTC+2 on Sunday 28 March at 02:00.
/// The center is closed on Sundays, so the days that matter are the Friday before and the Monday after.
/// </summary>
public class CenterTimeZoneDstTests
{
    [Fact]
    public void FridayBeforeTheSwitch_IsUtcPlusOne()
    {
        var startsAt = CenterTimeZone.FromLocal(new DateOnly(2027, 3, 26), new TimeOnly(9, 0));

        Assert.Equal(new DateTimeOffset(2027, 3, 26, 8, 0, 0, TimeSpan.Zero), startsAt);
    }

    [Fact]
    public void MondayAfterTheSwitch_IsUtcPlusTwo()
    {
        var startsAt = CenterTimeZone.FromLocal(new DateOnly(2027, 3, 29), new TimeOnly(9, 0));

        Assert.Equal(new DateTimeOffset(2027, 3, 29, 7, 0, 0, TimeSpan.Zero), startsAt);
    }

    [Fact]
    public void MondayAfterTheSwitch_StillPassesWorkingHours()
    {
        // 07:00–22:00 local must still be accepted when it is 05:00–20:00 UTC (summer).
        var day = new DateOnly(2027, 3, 29);

        var exception = Record.Exception(() =>
            WorkingHours.EnsureWithin(
                CenterTimeZone.FromLocal(day, new TimeOnly(7, 0)),
                CenterTimeZone.FromLocal(day, new TimeOnly(22, 0))
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public void SundayOfTheSwitch_IsClosed()
    {
        var day = new DateOnly(2027, 3, 28);

        var exception = Assert.Throws<DomainException>(() =>
            WorkingHours.EnsureWithin(
                CenterTimeZone.FromLocal(day, new TimeOnly(10, 0)),
                CenterTimeZone.FromLocal(day, new TimeOnly(12, 0))
            )
        );

        Assert.Equal("BR04", exception.RuleCode);
    }

    [Fact]
    public void BackToWinterTime_InOctober_IsUtcPlusOneAgain()
    {
        // 2027: summer time ends on Sunday 31 October. Monday 1 November is UTC+1 again.
        var startsAt = CenterTimeZone.FromLocal(new DateOnly(2027, 11, 1), new TimeOnly(9, 0));

        Assert.Equal(new DateTimeOffset(2027, 11, 1, 8, 0, 0, TimeSpan.Zero), startsAt);
    }
}
