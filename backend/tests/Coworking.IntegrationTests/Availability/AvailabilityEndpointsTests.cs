using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coworking.Application.Auth;
using Coworking.Application.Availability;
using Coworking.Application.Availability.Dtos;
using Coworking.Domain.Common;
using Coworking.Domain.Enums;
using Coworking.IntegrationTests.Fixtures;
using static Coworking.IntegrationTests.Fixtures.TestSeed;

namespace Coworking.IntegrationTests.Availability;

/// <summary>
/// GET /api/availability end-to-end (FR02, UC02): what counts as free (BR01 overlap rule incl.
/// the touching boundary, TC03/TC04), what is never offered (inactive workspace or room, fixed
/// desks), filters, the BR04 and "in the past" rules, input validation and public access.
/// </summary>
/// <remarks>
/// The database is shared with every other test in this class, so results contain other rooms
/// too. Tests therefore only look for THEIR OWN workspace ids. Dates are computed relative to
/// today (next weekday, next Saturday...), because the API refuses the past and more than 90 days ahead.
/// </remarks>
public class AvailabilityEndpointsTests(PostgresApiFactory factory)
    : IClassFixture<PostgresApiFactory>
{
    /// <summary>Enums travel as names ("HotDesk").</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // ------------------------------------------------------------------ access (NFR01: public)

    public static TheoryData<string?> Callers() =>
        new() { null, Roles.Member, Roles.Receptionist, Roles.Manager, Roles.Administrator };

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task NFR01_EveryoneCanSearch_IncludingAnonymousVisitors(string? role)
    {
        var url = SearchUrl(NextWeekday(), "10:00", "12:00");

        HttpResponseMessage response;
        if (role is null)
        {
            using var anonymous = factory.CreateHttpsClient();
            response = await anonymous.GetAsync(url);
        }
        else
        {
            using var caller = await factory.CreateClientAsAsync(role);
            response = await caller.Client.GetAsync(url);
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------ what is free (BR01)

    [Fact]
    public async Task FR02_FreeWorkspace_IsListedUnderItsRoom_WithUtcPeriod()
    {
        var room = await factory.SeedRoomAsync($"Hall {UniqueTag()}", floor: 3);
        var desk = await factory.SeedWorkspaceAsync(room.Id);
        var day = NextWeekday();

        var result = await SearchAsync(day, "09:00", "11:30");

        var foundRoom = Assert.Single(result.Rooms, r => r.Id == room.Id);
        Assert.Equal(room.Name, foundRoom.Name);
        Assert.Equal(3, foundRoom.Floor);
        Assert.Contains(
            foundRoom.Workspaces,
            w => w.Id == desk.Id && w.Type == WorkspaceType.HotDesk
        );
        Assert.Equal(CenterTimeZone.FromLocal(day, new TimeOnly(9, 0)), result.StartsAt);
        Assert.Equal(CenterTimeZone.FromLocal(day, new TimeOnly(11, 30)), result.EndsAt);
        Assert.Equal(TimeSpan.Zero, result.StartsAt.Offset); // UTC
        Assert.Equal(result.Rooms.Sum(r => r.Workspaces.Count), result.TotalFree);
    }

    [Theory]
    [InlineData("09:00", "11:00")] // ends inside 10–12
    [InlineData("11:00", "13:00")] // starts inside 10–12 (TC03)
    [InlineData("10:00", "12:00")] // exactly the same period
    [InlineData("09:00", "13:00")] // contains 10–12
    [InlineData("10:30", "11:30")] // inside 10–12
    public async Task TC03_OverlappingConfirmedReservation_HidesTheWorkspace(string from, string to)
    {
        var day = NextWeekday();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var desk = await factory.SeedWorkspaceAsync(room.Id);
        await factory.SeedReservationAsync(desk.Id, day, new TimeOnly(10, 0), new TimeOnly(12, 0));

        var result = await SearchAsync(day, from, to);

        Assert.DoesNotContain(AllWorkspaceIds(result), id => id == desk.Id);
    }

    [Theory]
    [InlineData("08:00", "10:00")] // ends exactly when the reservation starts
    [InlineData("12:00", "14:00")] // starts exactly when the reservation ends
    public async Task TC04_TouchingReservation_LeavesTheWorkspaceFree(string from, string to)
    {
        // Half-open periods [start, end): 10:00–12:00 and 12:00–14:00 don't overlap.
        var day = NextWeekday();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var desk = await factory.SeedWorkspaceAsync(room.Id);
        await factory.SeedReservationAsync(desk.Id, day, new TimeOnly(10, 0), new TimeOnly(12, 0));

        var result = await SearchAsync(day, from, to);

        Assert.Contains(AllWorkspaceIds(result), id => id == desk.Id);
    }

    [Fact]
    public async Task CancelledReservation_DoesNotBlock()
    {
        var day = NextWeekday();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var desk = await factory.SeedWorkspaceAsync(room.Id);
        await factory.SeedReservationAsync(
            desk.Id,
            day,
            new TimeOnly(10, 0),
            new TimeOnly(12, 0),
            cancelled: true
        );

        var result = await SearchAsync(day, "10:00", "12:00");

        Assert.Contains(AllWorkspaceIds(result), id => id == desk.Id);
    }

    [Fact]
    public async Task ReservationOnAnotherDay_DoesNotBlock()
    {
        var day = NextWeekday();
        var nextDay = day.AddDays(1);
        while (nextDay.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            nextDay = nextDay.AddDays(1);
        }

        var room = await factory.SeedRoomAsync(UniqueTag());
        var desk = await factory.SeedWorkspaceAsync(room.Id);
        await factory.SeedReservationAsync(
            desk.Id,
            nextDay,
            new TimeOnly(10, 0),
            new TimeOnly(12, 0)
        );

        var result = await SearchAsync(day, "10:00", "12:00");

        Assert.Contains(AllWorkspaceIds(result), id => id == desk.Id);
    }

    // ------------------------------------------------------------------ what is never offered

    [Fact]
    public async Task InactiveWorkspace_InactiveRoom_AndFixedDesk_AreNeverListed()
    {
        var openRoom = await factory.SeedRoomAsync(UniqueTag());
        var offered = await factory.SeedWorkspaceAsync(openRoom.Id);
        var inactive = await factory.SeedWorkspaceAsync(openRoom.Id, isActive: false);
        var fixedDesk = await factory.SeedWorkspaceAsync(openRoom.Id, WorkspaceType.FixedDesk);
        var closedRoom = await factory.SeedRoomAsync(UniqueTag(), workspaces: 1, isActive: false);

        var result = await SearchAsync(NextWeekday(), "10:00", "12:00");
        var ids = AllWorkspaceIds(result);

        Assert.Contains(offered.Id, ids);
        Assert.DoesNotContain(inactive.Id, ids);
        Assert.DoesNotContain(fixedDesk.Id, ids);
        Assert.DoesNotContain(result.Rooms, r => r.Id == closedRoom.Id);
    }

    [Fact]
    public async Task RoomWithoutFreeWorkspaces_IsLeftOut()
    {
        var day = NextWeekday();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var desk = await factory.SeedWorkspaceAsync(room.Id);
        await factory.SeedReservationAsync(desk.Id, day, new TimeOnly(10, 0), new TimeOnly(12, 0));

        var result = await SearchAsync(day, "10:00", "12:00");

        Assert.DoesNotContain(result.Rooms, r => r.Id == room.Id);
    }

    // ------------------------------------------------------------------ filters, order

    [Fact]
    public async Task TypeAndMinCapacity_Filter_AndWorkspacesAreSortedByCode()
    {
        var room = await factory.SeedRoomAsync(UniqueTag());
        var tag = UniqueTag();
        var big = await factory.SeedWorkspaceAsync(
            room.Id,
            WorkspaceType.MeetingRoom,
            12,
            code: $"{tag}-B"
        );
        var medium = await factory.SeedWorkspaceAsync(
            room.Id,
            WorkspaceType.MeetingRoom,
            6,
            code: $"{tag}-A"
        );
        var small = await factory.SeedWorkspaceAsync(
            room.Id,
            WorkspaceType.MeetingRoom,
            4,
            code: $"{tag}-C"
        );
        var office = await factory.SeedWorkspaceAsync(
            room.Id,
            WorkspaceType.Office,
            8,
            code: $"{tag}-O"
        );

        var result = await SearchAsync(
            NextWeekday(),
            "10:00",
            "12:00",
            "&type=MeetingRoom&minCapacity=6"
        );

        Assert.All(
            result.Rooms.SelectMany(r => r.Workspaces),
            w => Assert.Equal(WorkspaceType.MeetingRoom, w.Type)
        );
        Assert.All(result.Rooms.SelectMany(r => r.Workspaces), w => Assert.True(w.Capacity >= 6));
        var ours = Assert.Single(result.Rooms, r => r.Id == room.Id).Workspaces.Select(w => w.Id);
        Assert.Equal(new[] { medium.Id, big.Id }, ours); // A before B; C too small, O is an office
        Assert.DoesNotContain(small.Id, AllWorkspaceIds(result));
        Assert.DoesNotContain(office.Id, AllWorkspaceIds(result));
    }

    // ------------------------------------------------------------------ business rules (422)

    [Fact]
    public async Task BR04_Sunday_Returns422()
    {
        var response = await GetAsync(SearchUrl(Next(DayOfWeek.Sunday), "10:00", "12:00"));

        await AssertRuleCodeAsync(response, "BR04");
    }

    [Fact]
    public async Task BR04_SaturdayBeforeOpening_Returns422()
    {
        // Saturdays the center opens at 09:00.
        var response = await GetAsync(SearchUrl(Next(DayOfWeek.Saturday), "08:00", "10:00"));

        await AssertRuleCodeAsync(response, "BR04");
    }

    [Fact]
    public async Task BR04_SaturdayInsideHours_Returns200()
    {
        var response = await GetAsync(SearchUrl(Next(DayOfWeek.Saturday), "09:00", "17:00"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BR04_WeekdayAfterClosing_Returns422()
    {
        var response = await GetAsync(SearchUrl(NextWeekday(), "21:00", "23:00"));

        await AssertRuleCodeAsync(response, "BR04");
    }

    [Fact]
    public async Task BR04_ShorterThanOneHour_Returns422()
    {
        var response = await GetAsync(SearchUrl(NextWeekday(), "10:00", "10:45"));

        await AssertRuleCodeAsync(response, "BR04");
    }

    [Fact]
    public async Task PeriodInThePast_Returns422InPast()
    {
        // The most recent weekday at least 2 days ago, inside opening hours: BR04 passes, so the
        // "already ended" rule is what answers.
        var day = CenterTimeZone.ToLocalDate(DateTimeOffset.UtcNow).AddDays(-2);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            day = day.AddDays(-1);
        }

        var response = await GetAsync(SearchUrl(day, "10:00", "12:00"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(AvailabilityErrorCodes.InPast, await ReadCodeAsync(response));
    }

    // ------------------------------------------------------------------ malformed input (400)

    [Theory]
    [InlineData("type=FixedDesk", "type")]
    [InlineData("type=Sofa", null)] // unknown enum name: rejected by model binding
    [InlineData("minCapacity=0", "minCapacity")]
    public async Task InvalidFilter_Returns400(string extra, string? field)
    {
        var response = await GetAsync(SearchUrl(NextWeekday(), "10:00", "12:00") + "&" + extra);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        if (field is not null)
        {
            Assert.True(await HasFieldErrorAsync(response, field));
        }
    }

    [Theory]
    [InlineData("12:00", "10:00", "to")] // end before start
    [InlineData("10:00", "10:00", "to")] // zero length
    [InlineData("10:10", "12:00", "from")] // not a quarter hour
    public async Task InvalidTimes_Return400(string from, string to, string field)
    {
        var response = await GetAsync(SearchUrl(NextWeekday(), from, to));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await HasFieldErrorAsync(response, field));
    }

    [Fact]
    public async Task MoreThan90DaysAhead_Returns400()
    {
        var response = await GetAsync(SearchUrl(NextWeekday(minDaysAhead: 92), "10:00", "12:00"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await HasFieldErrorAsync(response, "date"));
    }

    [Fact]
    public async Task MissingParameters_Return400ForEachOne()
    {
        var response = await GetAsync("/api/availability");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await HasFieldErrorAsync(response, "date"));
        Assert.True(await HasFieldErrorAsync(response, "from"));
        Assert.True(await HasFieldErrorAsync(response, "to"));
    }

    // ------------------------------------------------------------------ helpers

    private static string SearchUrl(DateOnly date, string from, string to) =>
        $"/api/availability?date={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&from={from}&to={to}";

    private async Task<HttpResponseMessage> GetAsync(string url)
    {
        using var client = factory.CreateHttpsClient();
        return await client.GetAsync(url);
    }

    private async Task<AvailabilityResponse> SearchAsync(
        DateOnly date,
        string from,
        string to,
        string extra = ""
    )
    {
        var response = await GetAsync(SearchUrl(date, from, to) + extra);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AvailabilityResponse>(Json))!;
    }

    private static List<int> AllWorkspaceIds(AvailabilityResponse result) =>
        [.. result.Rooms.SelectMany(r => r.Workspaces).Select(w => w.Id)];

    private static async Task AssertRuleCodeAsync(HttpResponseMessage response, string ruleCode)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ruleCode, body.GetProperty("ruleCode").GetString());
    }

    /// <summary>Validation errors are keyed camelCase (date, from, to...).</summary>
    private static async Task<bool> HasFieldErrorAsync(HttpResponseMessage response, string field)
    {
        // ReadAsStringAsync, not ReadFromJsonAsync: a test calls this several times on the same response,
        // and ReadFromJsonAsync closes the stream after the first read. The body is buffered, so a string re-reads fine.
        var body = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync()
        );
        return body.TryGetProperty("errors", out var errors)
            && errors
                .EnumerateObject()
                .Any(e => string.Equals(e.Name, field, StringComparison.OrdinalIgnoreCase));
    }
}
