using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coworking.Application.Auth;
using Coworking.Application.Auth.Dtos;
using Coworking.Application.Availability.Dtos;
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
        var first = await DemoDataSeeder.SeedAsync(factory.Services, new DemoDataOptions { DemoData = true });

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
        Assert.Equal(new DemoDataSeedResult(0, 0, 0, 2), second); // receptionist already existed
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
        Assert.Equal("Student Monthly", (await db.MembershipTypes.SingleAsync(t => !t.IsActive)).Name);

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
    }

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
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, DemoPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.User.Role;
    }
}
