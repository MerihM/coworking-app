using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coworking.Application.Auth;
using Coworking.Application.Auth.Dtos;
using Coworking.Application.Availability.Dtos;
using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Coworking.Infrastructure.Seeding;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coworking.IntegrationTests.Seeding;

/// <summary>
/// The demo data seeder (CW-22). Its own test class = its own, EMPTY database container – the
/// seeder only fills empty tables, so it must not share a database with tests that create rooms.
/// </summary>
/// <remarks>
/// The main scenario is ONE test on purpose: xUnit doesn't guarantee the order of tests in a
/// class, and "first run, second run, third run" only means something in a fixed order.
/// </remarks>
public class DemoDataSeederTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string DemoPassword = "Demo12345";

    /// <summary>The availability response contains enums as names ("HotDesk").</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Seed_FillsEmptyTablesOnce_KeepsExistingAccounts_AndIsIdempotent()
    {
        // An account the developer created by hand before the seeder existed.
        await SeedEmployeeAsync("receptionist@coworking.ba", "Original", EmployeeRole.Receptionist);

        // 1st run WITHOUT a demo password: the center and prices, but no accounts.
        var first = await DemoDataSeeder.SeedAsync(
            factory.Services,
            new DemoDataOptions { DemoData = true }
        );

        // 2nd run WITH the password: rooms and prices exist now → only the missing accounts.
        var second = await DemoDataSeeder.SeedAsync(
            factory.Services,
            new DemoDataOptions { DemoData = true, DemoPassword = DemoPassword }
        );

        // 3rd run: everything is there → nothing to do.
        var third = await DemoDataSeeder.SeedAsync(
            factory.Services,
            new DemoDataOptions { DemoData = true, DemoPassword = DemoPassword }
        );

        Assert.Equal(new DemoDataSeedResult(6, 35, 4, 0), first);
        // The demo member is created in this run, so it also gets its paid demo membership (CW-26)
        // and its two upcoming reservations (CW-29). The history members (CW-32) need the password
        // too, so they also come now – exactly as many payments and visits as the plan says.
        var plan = DemoHistory.Plan(Today(), DemoTypeDurations());
        Assert.Equal(
            new DemoDataSeedResult(0, 0, 0, 2)
            {
                Memberships = 1,
                Reservations = 2,
                HistoryMembers = DemoData.HistoryMembers.Count,
                HistoryPayments = plan.Purchases.Count,
                HistoryReservations = plan.Visits.Count,
            },
            second
        ); // receptionist already existed
        Assert.Equal(DemoDataSeedResult.Nothing, third);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();

        // The center from the design document (chapter 1.1).
        Assert.Equal(6, await db.Rooms.CountAsync());
        Assert.Equal(24, await db.Workspaces.CountAsync(w => w.Type == WorkspaceType.HotDesk));
        Assert.Equal(6, await db.Workspaces.CountAsync(w => w.Type == WorkspaceType.FixedDesk));
        Assert.Equal(3, await db.Workspaces.CountAsync(w => w.Type == WorkspaceType.Office));
        Assert.Equal(2, await db.Workspaces.CountAsync(w => w.Type == WorkspaceType.MeetingRoom));

        // Price list: 4 types, the student one not offered.
        Assert.Equal(4, await db.MembershipTypes.CountAsync());
        Assert.Equal(
            "Student Monthly",
            (await db.MembershipTypes.SingleAsync(t => !t.IsActive)).Name
        );

        // The hand-made receptionist was not overwritten.
        Assert.Equal(
            "Original",
            (await db.Employees.SingleAsync(e => e.Email == "receptionist@coworking.ba")).FirstName
        );

        // The seeded accounts work with the demo password, with the right roles.
        Assert.Equal(Roles.Manager, await LoginRoleAsync("manager@coworking.ba"));
        Assert.Equal(Roles.Member, await LoginRoleAsync("member@coworking.ba"));

        // End to end: the public search sees the demo center. Fixed desks are not offered (CW-21):
        // 24 hot desks + 3 offices + 2 meeting rooms = 29.
        var day = TestSeed.NextWeekday().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using var anonymous = factory.CreateHttpsClient();
        var search = await anonymous.GetFromJsonAsync<AvailabilityResponse>(
            $"/api/availability?date={day}&from=10:00&to=12:00",
            Json
        );
        Assert.Equal(29, search!.TotalFree);
        Assert.Equal("Open Space", search.Rooms[0].Name); // floor 1 comes first

        // The history is really in the past and went through the rules: no reservation of a
        // history member starts today or later, and the cancelled ones are stored as Cancelled.
        var historyEmails = DemoData.HistoryMembers.Select(m => m.Email).ToList();
        var startOfToday = CenterTimeZone.FromLocal(Today(), TimeOnly.MinValue);
        Assert.False(
            await db.Reservations.AnyAsync(r => historyEmails.Contains(r.Member.Email) && r.StartsAt >= startOfToday)
        );
        Assert.Equal(
            plan.Visits.Count(v => v.Cancelled),
            await db.Reservations.CountAsync(r =>
                historyEmails.Contains(r.Member.Email) && r.Status == ReservationStatus.Cancelled
            )
        );
    }

    [Fact]
    public void DemoHistory_Plan_FollowsTheBusinessRules()
    {
        // A fixed Friday, so the test doesn't depend on the day it runs.
        var today = new DateOnly(2026, 11, 27);
        var plan = DemoHistory.Plan(today, DemoTypeDurations());
        var durations = DemoTypeDurations();

        Assert.NotEmpty(plan.Visits);
        Assert.All(
            plan.Visits,
            visit =>
            {
                // In the past window, never on a Sunday (BR04), and inside the opening hours.
                Assert.InRange(visit.Day, today.AddDays(-DemoData.HistoryDays), today.AddDays(-1));
                Assert.True(
                    WorkingHours.GetHours(visit.Day.DayOfWeek) is { } hours
                        && visit.From >= hours.Open
                        && visit.To <= hours.Close
                );

                // BR02: some purchase of the same member covers the visit day.
                Assert.Contains(
                    plan.Purchases,
                    p =>
                        p.Email == visit.Email
                        && p.Day <= visit.Day
                        && visit.Day < p.Day.AddDays(durations[p.MembershipTypeName])
                );

                // BR03: a cancelled visit starts late enough to be cancelled at 07:10.
                Assert.True(!visit.Cancelled || visit.From >= new TimeOnly(9, 10));
            }
        );

        // BR01: no two kept visits overlap on the same workspace.
        var kept = plan.Visits.Where(v => !v.Cancelled).ToList();
        foreach (var visit in kept)
        {
            Assert.DoesNotContain(
                kept,
                other =>
                    !ReferenceEquals(other, visit)
                    && other.WorkspaceCode == visit.WorkspaceCode
                    && other.Day == visit.Day
                    && other.From < visit.To
                    && visit.From < other.To
            );
        }

        // A Daily member pays on every visit day, a Monthly member once in 4 weeks.
        var monthlyEmail = DemoData.HistoryMembers.First(m => m.MembershipTypeName == "Monthly").Email;
        Assert.Single(plan.Purchases, p => p.Email == monthlyEmail);
        var dailyEmail = DemoData.HistoryMembers.First(m => m.MembershipTypeName == "Daily").Email;
        Assert.Equal(
            plan.Visits.Where(v => v.Email == dailyEmail).Select(v => v.Day).Distinct().Count(),
            plan.Purchases.Count(p => p.Email == dailyEmail)
        );
    }

    private static DateOnly Today() => CenterTimeZone.ToLocalDate(DateTimeOffset.UtcNow);

    /// <summary>Durations of the ACTIVE demo membership types – the ones the seeder can sell.</summary>
    private static Dictionary<string, int> DemoTypeDurations() =>
        DemoData.MembershipTypes.Where(t => t.IsActive).ToDictionary(t => t.Name, t => t.DurationDays);

    [Fact]
    public async Task SeedDemoData_SwitchedOff_DoesNothing()
    {
        // The test configuration sets Seed:DemoData=false (TestAuthSettings), like production.
        var result = await factory.Services.SeedDemoDataAsync();

        Assert.Equal(DemoDataSeedResult.Nothing, result);
    }

    [Fact]
    public void DemoData_MatchesTheDesignDocument()
    {
        // A quick guard for whoever edits DemoData: 30 open-space desks, 3 offices, 2 meeting rooms.
        var workspaces = DemoData.Rooms.SelectMany(r => r.Workspaces).ToList();

        Assert.Equal(30, workspaces.Count(w => w.Code.StartsWith("OS-", StringComparison.Ordinal)));
        Assert.Equal(3, workspaces.Count(w => w.Type == WorkspaceType.Office));
        Assert.Equal(2, workspaces.Count(w => w.Type == WorkspaceType.MeetingRoom));
        Assert.Equal(workspaces.Count, workspaces.Select(w => w.Code).Distinct().Count()); // codes unique
    }

    private async Task SeedEmployeeAsync(string email, string firstName, EmployeeRole role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        db.Employees.Add(new Employee(firstName, "Hand-made", email, "hash", role));
        await db.SaveChangesAsync();
    }

    private async Task<string> LoginRoleAsync(string email)
    {
        using var client = factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, DemoPassword)
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.User.Role;
    }
}
