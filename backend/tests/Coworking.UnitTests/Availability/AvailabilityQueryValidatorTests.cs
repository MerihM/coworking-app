using Coworking.Application.Availability.Dtos;
using Coworking.Application.Availability.Validators;
using Coworking.Domain.Enums;

namespace Coworking.UnitTests.Availability;

/// <summary>
/// Shape rules of the availability search (FR02), with a FIXED "now", so the 90-day limit can be
/// tested at its exact edge – something the integration tests (real clock) can't do reliably.
/// </summary>
public class AvailabilityQueryValidatorTests
{
    /// <summary>"Today" = Thursday 2026-11-26 in Sarajevo (10:00 local = 09:00 UTC).</summary>
    private static readonly DateOnly Today = new(2026, 11, 26);

    private readonly AvailabilityQueryValidator _validator = new(
        new FixedTimeProvider(new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero))
    );

    [Fact]
    public void ValidSearch_Passes()
    {
        Assert.True(_validator.Validate(Query()).IsValid);
    }

    [Fact]
    public void Date_ExactlyNinetyDaysAhead_Passes_NinetyOne_Fails()
    {
        Assert.True(_validator.Validate(Query(date: Today.AddDays(90))).IsValid);
        Assert.False(_validator.Validate(Query(date: Today.AddDays(91))).IsValid);
    }

    [Fact]
    public void Date_InThePast_PassesHere()
    {
        // "Already ended" is a BUSINESS rule (422 availability.in_past), checked by the service
        // after BR04 – not a shape error (400).
        Assert.True(_validator.Validate(Query(date: Today.AddDays(-7))).IsValid);
    }

    [Theory]
    [InlineData(9, 0, true)]
    [InlineData(9, 15, true)]
    [InlineData(9, 45, true)]
    [InlineData(9, 10, false)]
    [InlineData(9, 59, false)]
    public void From_MustBeOnAQuarterHour(int hour, int minute, bool valid)
    {
        Assert.Equal(valid, _validator.Validate(Query(from: new TimeOnly(hour, minute))).IsValid);
    }

    [Fact]
    public void To_MustBeAfterFrom()
    {
        Assert.False(_validator.Validate(Query(from: new TimeOnly(12, 0), to: new TimeOnly(10, 0))).IsValid);
        Assert.False(_validator.Validate(Query(from: new TimeOnly(12, 0), to: new TimeOnly(12, 0))).IsValid);
    }

    [Theory]
    [InlineData(WorkspaceType.HotDesk, true)]
    [InlineData(WorkspaceType.Office, true)]
    [InlineData(WorkspaceType.MeetingRoom, true)]
    [InlineData(WorkspaceType.FixedDesk, false)]
    public void Type_FixedDeskIsNotSearchable(WorkspaceType type, bool valid)
    {
        Assert.Equal(valid, _validator.Validate(Query(type: type)).IsValid);
    }

    [Fact]
    public void MissingValues_EachGetAnError()
    {
        var result = _validator.Validate(new AvailabilityQuery());

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AvailabilityQuery.Date));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AvailabilityQuery.From));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AvailabilityQuery.To));
    }

    private static AvailabilityQuery Query(
        DateOnly? date = null,
        TimeOnly? from = null,
        TimeOnly? to = null,
        WorkspaceType? type = null
    ) =>
        new()
        {
            Date = date ?? Today.AddDays(5),
            From = from ?? new TimeOnly(10, 0),
            To = to ?? new TimeOnly(12, 0),
            Type = type,
        };

    /// <summary>A clock that always says the same moment (no extra test package needed).</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
