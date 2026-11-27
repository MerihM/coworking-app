using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coworking.Application.Auth;
using Coworking.Application.Availability.Dtos;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Reservations;
using Coworking.Application.Reservations.Dtos;
using Coworking.Domain.Common;
using Coworking.Domain.Enums;
using Coworking.IntegrationTests.Fixtures;
using static Coworking.IntegrationTests.Fixtures.TestSeed;

namespace Coworking.IntegrationTests.Reservations;

/// <summary>
/// Reservations end-to-end (CW-29): the test cases TC01–TC06 from the design document (table 16)
/// through the Web API, UC06 alternatives, cancelling, the lists and the NFR01 matrix.
/// </summary>
/// <remarks>
/// All test classes of this fixture share one database, so every test reserves workspaces in its
/// OWN room (unique name) and never assumes that other workspaces are free or taken.
/// </remarks>
public class ReservationsEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    /// <summary>Enums as names in BOTH directions, like the API (allowIntegerValues: false there).</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly TimeOnly T10 = new(10, 0);
    private static readonly TimeOnly T11 = new(11, 0);
    private static readonly TimeOnly T12 = new(12, 0);
    private static readonly TimeOnly T13 = new(13, 0);
    private static readonly TimeOnly T14 = new(14, 0);

    // ------------------------------------------------------------------ access control (NFR01)

    public static TheoryData<string, string?, HttpStatusCode> AccessMatrix()
    {
        var data = new TheoryData<string, string?, HttpStatusCode>();
        string?[] callers = [null, Roles.Member, Roles.Receptionist, Roles.Manager, Roles.Administrator];
        foreach (var caller in callers)
        {
            data.Add(
                "/api/reservations",
                caller,
                caller is null ? HttpStatusCode.Unauthorized
                    : caller is Roles.Receptionist or Roles.Manager ? HttpStatusCode.OK
                    : HttpStatusCode.Forbidden
            );
            data.Add(
                "/api/reservations/mine",
                caller,
                caller is null ? HttpStatusCode.Unauthorized
                    : caller == Roles.Member ? HttpStatusCode.OK
                    : HttpStatusCode.Forbidden
            );
            // No parameters: a member gets past the policy and is stopped by the validator (400).
            data.Add(
                "/api/reservations/alternatives",
                caller,
                caller is null ? HttpStatusCode.Unauthorized
                    : caller == Roles.Member ? HttpStatusCode.BadRequest
                    : HttpStatusCode.Forbidden
            );
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AccessMatrix))]
    public async Task NFR01_EachRoleReachesOnlyItsOwnFunctions(string url, string? role, HttpStatusCode expected)
    {
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

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task NFR01_EmployeesCantReserve()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var desk = await SeedDeskAsync();

        var response = await ReserveAsync(reception.Client, desk, NextWeekday(), T10, T12);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------ TC01–TC05 (UC04)

    [Fact]
    public async Task TC01_ActiveMembership_FreeWorkspace_WeekdayTenToTwelve_IsConfirmed()
    {
        using var member = await MemberWithMembershipAsync();
        var desk = await SeedDeskAsync();
        var day = NextWeekday();

        var response = await ReserveAsync(member.Client, desk, day, T10, T12);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ReservationResponse>(Json))!;
        Assert.Equal(ReservationStatus.Confirmed, created.Status);
        Assert.Equal(desk, created.WorkspaceId);
        Assert.Equal(member.User.Id, created.MemberId);
        Assert.Equal(CenterTimeZone.FromLocal(day, T10), created.StartsAt); // stored as a UTC instant
        Assert.Equal((day, T10, T12), (created.Date, created.From, created.To)); // shown in local time
        Assert.True(created.CanMemberCancel);

        // The member sees it among the upcoming reservations (FR06) ...
        var upcoming = await ListMineAsync(member.Client, ReservationScope.Upcoming);
        Assert.Contains(upcoming.Items, r => r.Id == created.Id);

        // ... and the workspace is no longer free in the search (BR01 via FR02).
        using var anonymous = factory.CreateHttpsClient();
        var search = (
            await anonymous.GetFromJsonAsync<AvailabilityResponse>($"/api/availability?date={Iso(day)}&from=10:00&to=12:00", Json)
        )!;
        Assert.DoesNotContain(search.Rooms.SelectMany(r => r.Workspaces), w => w.Id == desk);
    }

    [Fact]
    public async Task TC02_ExpiredMembership_IsRefusedWithBr02_AndNothingIsCreated()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        await factory.GivePaidMembershipAsync(member.User.Id, paidDaysAgo: 40, durationDays: 30); // ended 10 days ago
        var desk = await SeedDeskAsync();

        var response = await ReserveAsync(member.Client, desk, NextWeekday(), T10, T12);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("BR02", await ReadRuleCodeAsync(response)); // UI: message + "buy a membership"
        Assert.Equal(0, (await ListMineAsync(member.Client, ReservationScope.Upcoming)).TotalCount);
    }

    [Fact]
    public async Task TC02_NoMembershipAtAll_IsRefusedWithBr02()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        var desk = await SeedDeskAsync();

        var response = await ReserveAsync(member.Client, desk, NextWeekday(), T10, T12);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("BR02", await ReadRuleCodeAsync(response));
    }

    [Fact]
    public async Task TC03_OverlappingReservation_IsRefused_AndAlternativesAreOffered()
    {
        var room = await factory.SeedRoomAsync($"Hall {UniqueTag()}");
        var a1 = await factory.SeedWorkspaceAsync(room.Id);
        var a2 = await factory.SeedWorkspaceAsync(room.Id);
        var day = NextWeekday();
        await factory.SeedReservationAsync(a1.Id, day, T10, T12); // another member: A1 10–12

        using var member = await MemberWithMembershipAsync();

        // UC04 step 5 → flow 5a: A1 11–13 overlaps.
        var refused = await ReserveAsync(member.Client, a1.Id, day, T11, T13);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var body = await refused.Content.ReadAsStringAsync(); // read once, check both fields
        Assert.Contains($"\"code\":\"{ReservationErrorCodes.SlotTaken}\"", body, StringComparison.Ordinal);
        Assert.Contains("\"ruleCode\":\"BR01\"", body, StringComparison.Ordinal);

        // UC06: the system offers another free workspace or a free time.
        var alternatives = (
            await member.Client.GetFromJsonAsync<ReservationAlternativesResponse>(
                $"/api/reservations/alternatives?workspaceId={a1.Id}&date={Iso(day)}&from=11:00&to=13:00",
                Json
            )
        )!;
        Assert.Equal(a2.Id, alternatives.OtherWorkspaces[0].Id); // same room first
        Assert.DoesNotContain(alternatives.OtherWorkspaces, w => w.Id == a1.Id);
        var firstSlot = alternatives.NextSlots[0];
        Assert.Equal((day, T12, new TimeOnly(14, 0)), (firstSlot.Date, firstSlot.From, firstSlot.To)); // same length, right after

        // The member accepts the offered time → created (the "ponuda prihvaćena" branch).
        var accepted = await ReserveAsync(member.Client, a1.Id, firstSlot.Date, firstSlot.From, firstSlot.To);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task TC04_TouchingReservation_TwelveToFourteen_IsCreated()
    {
        var desk = await SeedDeskAsync();
        var day = NextWeekday();
        await factory.SeedReservationAsync(desk, day, T10, T12);
        using var member = await MemberWithMembershipAsync();

        var response = await ReserveAsync(member.Client, desk, day, T12, T14);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode); // [10,12) and [12,14) only touch
    }

    [Fact]
    public async Task TC05_SaturdaySixteenToEighteen_IsRefusedWithBr04()
    {
        using var member = await MemberWithMembershipAsync();
        var desk = await SeedDeskAsync();

        var response = await ReserveAsync(member.Client, desk, Next(DayOfWeek.Saturday), new TimeOnly(16, 0), new TimeOnly(18, 0));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("BR04", await ReadRuleCodeAsync(response)); // Saturday closes at 17:00
    }

    [Fact]
    public async Task BrokenRuleOrder_FollowsTheActivityDiagram_WorkingHoursBeforeMembership()
    {
        // No membership AND outside working hours: BR04 is checked first (decision 1 before 2).
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        var desk = await SeedDeskAsync();

        var response = await ReserveAsync(member.Client, desk, Next(DayOfWeek.Sunday), T10, T12);

        Assert.Equal("BR04", await ReadRuleCodeAsync(response));
    }

    [Fact]
    public async Task FixedDeskOrInactiveWorkspace_IsUnavailable_UnknownIs404_PastIs422()
    {
        using var member = await MemberWithMembershipAsync();
        var room = await factory.SeedRoomAsync($"Hall {UniqueTag()}");
        var fixedDesk = await factory.SeedWorkspaceAsync(room.Id, WorkspaceType.FixedDesk);
        var inactive = await factory.SeedWorkspaceAsync(room.Id, isActive: false);
        var desk = await factory.SeedWorkspaceAsync(room.Id);
        var day = NextWeekday();

        var fixedResponse = await ReserveAsync(member.Client, fixedDesk.Id, day, T10, T12);
        var inactiveResponse = await ReserveAsync(member.Client, inactive.Id, day, T10, T12);
        var unknownResponse = await ReserveAsync(member.Client, int.MaxValue, day, T10, T12);
        var yesterday = CenterTimeZone.ToLocalDate(DateTimeOffset.UtcNow).AddDays(-1);
        var pastResponse = await ReserveAsync(member.Client, desk.Id, yesterday, T10, T12);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, fixedResponse.StatusCode);
        Assert.Equal(ReservationErrorCodes.WorkspaceUnavailable, await ReadCodeAsync(fixedResponse));
        Assert.Equal(ReservationErrorCodes.WorkspaceUnavailable, await ReadCodeAsync(inactiveResponse));
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, pastResponse.StatusCode);
        Assert.Equal(ReservationErrorCodes.InPast, await ReadCodeAsync(pastResponse));
    }

    [Fact]
    public async Task InvalidInput_Is400_WithCamelCaseKeys()
    {
        using var member = await MemberWithMembershipAsync();
        var desk = await SeedDeskAsync();

        // 10:10 is not on a quarter hour; the end is before the start.
        var response = await ReserveAsync(member.Client, desk, NextWeekday(), new TimeOnly(10, 10), new TimeOnly(9, 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("from", out _));
        Assert.True(errors.TryGetProperty("to", out _));
    }

    // ------------------------------------------------------------------ TC06 + cancelling (UC07)

    [Fact]
    public async Task TC06_StartsInOneHour_MemberCantCancel_ReceptionistCan()
    {
        using var member = await factory.CreateClientAsAsync(Roles.Member);
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var desk = await SeedDeskAsync();

        // "Starts in one hour" from the REAL clock (whole minutes): inserted directly, see the helper.
        var nowMinute = DateTimeOffset.UtcNow;
        nowMinute = nowMinute.AddTicks(-(nowMinute.Ticks % TimeSpan.TicksPerMinute));
        var id = await factory.InsertReservationAsync(member.User.Id, desk, nowMinute.AddHours(1), nowMinute.AddHours(3));

        // The member: refused, with the 2-hour message (BR03).
        var byMember = await member.Client.PostAsync($"/api/reservations/mine/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, byMember.StatusCode);
        Assert.Equal("BR03", await ReadRuleCodeAsync(byMember));

        // The member's list says so in advance (the UI disables the button).
        var upcoming = await ListMineAsync(member.Client, ReservationScope.Upcoming);
        Assert.False(Assert.Single(upcoming.Items).CanMemberCancel);

        // The receptionist: allowed – the same reservation.
        var byReception = await reception.Client.PostAsync($"/api/reservations/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, byReception.StatusCode);

        var cancelled = (await reception.Client.GetFromJsonAsync<ReservationResponse>($"/api/reservations/{id}", Json))!;
        Assert.Equal(ReservationStatus.Cancelled, cancelled.Status);
        Assert.Equal("Test Receptionist", cancelled.CancelledByName);
    }

    [Fact]
    public async Task MemberCancelsInTime_SlotIsFreeAgain_SecondCancelIs409_OthersReservationIs404()
    {
        using var member = await MemberWithMembershipAsync();
        using var otherMember = await MemberWithMembershipAsync();
        var desk = await SeedDeskAsync();
        var day = NextWeekday();
        var created = (await (await ReserveAsync(member.Client, desk, day, T10, T12)).Content.ReadFromJsonAsync<ReservationResponse>(Json))!;

        // Another member can't even see it.
        var foreign = await otherMember.Client.PostAsync($"/api/reservations/mine/{created.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        // Days ahead → allowed (BR03).
        var cancel = await member.Client.PostAsync($"/api/reservations/mine/{created.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var again = await member.Client.PostAsync($"/api/reservations/mine/{created.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ReservationErrorCodes.NotCancellable, await ReadCodeAsync(again));

        // Cancelled → in the member's history, and the slot can be taken by someone else.
        var past = await ListMineAsync(member.Client, ReservationScope.Past);
        Assert.Equal(ReservationStatus.Cancelled, Assert.Single(past.Items).Status);
        Assert.Equal(HttpStatusCode.Created, (await ReserveAsync(otherMember.Client, desk, day, T10, T12)).StatusCode);
    }

    // ------------------------------------------------------------------ reception list

    [Fact]
    public async Task ReceptionList_FiltersByDateStatusAndSearch()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Manager);
        var room = await factory.SeedRoomAsync($"Hall {UniqueTag()}");
        var desk = await factory.SeedWorkspaceAsync(room.Id);
        var day = NextWeekday();
        var confirmed = await factory.SeedReservationAsync(desk.Id, day, T10, T12);
        var cancelled = await factory.SeedReservationAsync(desk.Id, day, T13, T14, cancelled: true);

        var all = await reception.Client.GetFromJsonAsync<PagedResult<ReservationResponse>>(
            $"/api/reservations?date={Iso(day)}&search={desk.Code}",
            Json
        );
        Assert.Collection( // soonest first
            all!.Items,
            r => Assert.Equal(confirmed.Id, r.Id),
            r => Assert.Equal(cancelled.Id, r.Id)
        );

        var onlyConfirmed = await reception.Client.GetFromJsonAsync<PagedResult<ReservationResponse>>(
            $"/api/reservations?date={Iso(day)}&search={desk.Code}&status=Confirmed",
            Json
        );
        var row = Assert.Single(onlyConfirmed!.Items);
        Assert.Equal((desk.Code, room.Name), (row.WorkspaceCode, row.RoomName));

        // Another day → not in the list.
        var nextDay = await reception.Client.GetFromJsonAsync<PagedResult<ReservationResponse>>(
            $"/api/reservations?date={Iso(day.AddDays(1))}&search={desk.Code}",
            Json
        );
        Assert.Empty(nextDay!.Items);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>A fresh member with a paid monthly membership from today (BR02 satisfied for 30 days).</summary>
    private async Task<AuthenticatedClient> MemberWithMembershipAsync()
    {
        var member = await factory.CreateClientAsAsync(Roles.Member);
        await factory.GivePaidMembershipAsync(member.User.Id);
        return member;
    }

    /// <summary>A hot desk in its own new room; returns the workspace id.</summary>
    private async Task<int> SeedDeskAsync()
    {
        var room = await factory.SeedRoomAsync($"Hall {UniqueTag()}");
        return (await factory.SeedWorkspaceAsync(room.Id)).Id;
    }

    private static Task<HttpResponseMessage> ReserveAsync(
        HttpClient client,
        int workspaceId,
        DateOnly day,
        TimeOnly from,
        TimeOnly to
    ) => client.PostAsJsonAsync("/api/reservations/mine", new CreateReservationRequest(workspaceId, day, from, to), Json);

    private static async Task<PagedResult<ReservationResponse>> ListMineAsync(HttpClient client, ReservationScope scope) =>
        (await client.GetFromJsonAsync<PagedResult<ReservationResponse>>($"/api/reservations/mine?scope={scope}", Json))!;

    private static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
