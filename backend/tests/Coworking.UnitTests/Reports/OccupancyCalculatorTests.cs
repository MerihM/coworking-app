using Coworking.Application.Reports;
using Coworking.Domain.Common;

namespace Coworking.UnitTests.Reports;

/// <summary>The FR11 report arithmetic (CW-32): opening hours, overlap, rate, period bounds.</summary>
public class OccupancyCalculatorTests
{
    // 2026-11-30 is a Monday, 2026-12-05 a Saturday, 2026-12-06 a Sunday.
    private static readonly DateOnly Monday = new(2026, 11, 30);
    private static readonly DateOnly Saturday = new(2026, 12, 5);
    private static readonly DateOnly Sunday = new(2026, 12, 6);

    [Fact]
    public void OpenHours_FollowTheWorkingHoursOfBr04()
    {
        Assert.Equal(15m, OccupancyCalculator.OpenHours(Monday, Monday)); // 07–22
        Assert.Equal(8m, OccupancyCalculator.OpenHours(Saturday, Saturday)); // 09–17
        Assert.Equal(0m, OccupancyCalculator.OpenHours(Sunday, Sunday)); // closed
        Assert.Equal(83m, OccupancyCalculator.OpenHours(Monday, Sunday)); // 5 × 15 + 8
        Assert.Equal(0m, OccupancyCalculator.OpenHours(Sunday, Monday)); // empty range
    }

    [Fact]
    public void OverlapHours_CountsOnlyThePartInsideThePeriod()
    {
        var (start, end) = OccupancyCalculator.PeriodBounds(Monday, Monday);
        DateTimeOffset At(int hour) => CenterTimeZone.FromLocal(Monday, new TimeOnly(hour, 0));

        Assert.Equal(2m, OccupancyCalculator.OverlapHours(At(10), At(12), start, end)); // inside
        Assert.Equal(1m, OccupancyCalculator.OverlapHours(At(10), At(12), start, At(11))); // cut at the end
        Assert.Equal(1m, OccupancyCalculator.OverlapHours(At(10), At(12), At(11), end)); // cut at the start
        Assert.Equal(0m, OccupancyCalculator.OverlapHours(At(10), At(12), At(12), end)); // touching (half-open)
        Assert.Equal(1.5m, OccupancyCalculator.OverlapHours(At(10), At(10).AddMinutes(90), start, end));
    }

    [Fact]
    public void PeriodBounds_AreLocalMidnights_InUtc()
    {
        var (start, end) = OccupancyCalculator.PeriodBounds(Monday, Monday);

        // Sarajevo is UTC+1 in winter: local midnight is 23:00 UTC of the day before.
        Assert.Equal(new DateTimeOffset(2026, 11, 29, 23, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(TimeSpan.FromHours(24), end - start);
    }

    [Theory]
    [InlineData(2, 15, 0.1333)]
    [InlineData(1, 15, 0.0667)] // rounded half away from zero
    [InlineData(15, 15, 1)]
    [InlineData(3, 0, 0)] // nothing available (e.g. only Sundays) → 0, not a division by zero
    public void Rate_IsReservedOverAvailable_WithFourDecimals(int reserved, int available, double expected)
    {
        Assert.Equal((decimal)expected, OccupancyCalculator.Rate(reserved, available));
    }
}
