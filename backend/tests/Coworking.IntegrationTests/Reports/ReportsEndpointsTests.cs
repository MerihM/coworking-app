using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coworking.Application.Auth;
using Coworking.Application.Reports.Dtos;
using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using static Coworking.IntegrationTests.Fixtures.TestSeed;

namespace Coworking.IntegrationTests.Reports;

/// <summary>
/// The FR11 / UC13 occupancy and revenue report (CW-32): TC08 and the NFR01 matrix against the
/// real endpoint, the occupancy arithmetic on the test's own workspaces, revenue, and validation.
/// </summary>
/// <remarks>
/// The database is shared by all tests of this class, so totals can contain other tests' data:
/// every test checks the rows of ITS OWN room, workspaces or membership type, and only lower
/// bounds (≥) on the totals.
/// </remarks>
public class ReportsEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // ------------------------------------------------------------------ TC08 + NFR01

    [Fact]
    public async Task TC08_MemberOpensReport_IsForbidden()
    {
        // TC08 (design document, table 16): a logged-in Member tries to open the occupancy and
        // revenue report → access is denied, because only the Manager may generate it.
        using var member = await factory.CreateClientAsAsync(Roles.Member);

        var response = await member.Client.GetAsync(ReportUrl(Today(), Today()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TC08_ManagerOpensReport_GetsTheReport()
    {
        using var manager = await factory.CreateClientAsAsync(Roles.Manager);

        var response = await manager.Client.GetAsync(ReportUrl(Today(), Today()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = (await response.Content.ReadFromJsonAsync<OccupancyRevenueReport>(Json))!;
        Assert.Equal((Today(), Today()), (report.From, report.To));
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(Roles.Member, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Receptionist, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Administrator, HttpStatusCode.Forbidden)] // roles don't inherit
    [InlineData(Roles.Manager, HttpStatusCode.OK)]
    public async Task NFR01_OnlyTheManagerOpensTheReport(string? role, HttpStatusCode expected)
    {
        HttpResponseMessage response;
        if (role is null)
        {
            using var anonymous = factory.CreateHttpsClient();
            response = await anonymous.GetAsync(ReportUrl(Today(), Today()));
        }
        else
        {
            using var caller = await factory.CreateClientAsAsync(role);
            response = await caller.Client.GetAsync(ReportUrl(Today(), Today()));
        }

        Assert.Equal(expected, response.StatusCode);
    }

    // ------------------------------------------------------------------ occupancy

    [Fact]
    public async Task Occupancy_OneWeekday_CountsReservedOverOpeningHours_AndIgnoresCancelled()
    {
        var room = await factory.SeedRoomAsync($"Report room {UniqueTag()}");
        var desk = await factory.SeedWorkspaceAsync(room.Id);
        var idle = await factory.SeedWorkspaceAsync(room.Id, WorkspaceType.MeetingRoom, capacity: 6);
        var fixedDesk = await factory.SeedWorkspaceAsync(room.Id, WorkspaceType.FixedDesk);
        var day = NextWeekday();
        await factory.SeedReservationAsync(desk.Id, day, new TimeOnly(10, 0), new TimeOnly(12, 0));
        await factory.SeedReservationAsync(desk.Id, day, new TimeOnly(13, 0), new TimeOnly(15, 0), cancelled: true);

        var report = await GetReportAsync(day, day);

        // Weekday 07–22 = 15 open hours per workspace; 2 reserved → 2 / 15 = 0.1333.
        var deskRow = Assert.Single(report.Occupancy.Workspaces, w => w.WorkspaceId == desk.Id);
        Assert.Equal(15m, deskRow.AvailableHours);
        Assert.Equal(2m, deskRow.ReservedHours);
        Assert.Equal(0.1333m, deskRow.OccupancyRate);
        Assert.Equal(1, deskRow.ReservationCount); // the cancelled one is not counted

        var idleRow = Assert.Single(report.Occupancy.Workspaces, w => w.WorkspaceId == idle.Id);
        Assert.Equal((0m, 0m, 0), (idleRow.ReservedHours, idleRow.OccupancyRate, idleRow.ReservationCount));

        // Fixed desks are assigned, never reserved → not part of the occupancy.
        Assert.DoesNotContain(report.Occupancy.Workspaces, w => w.WorkspaceId == fixedDesk.Id);

        // The room: 2 counted workspaces × 15 h, 2 h reserved → 2 / 30.
        var roomRow = Assert.Single(report.Occupancy.ByRoom, r => r.RoomId == room.Id);
        Assert.Equal((2, 30m, 2m, 0.0667m, 1), (roomRow.WorkspaceCount, roomRow.AvailableHours, roomRow.ReservedHours, roomRow.OccupancyRate, roomRow.ReservationCount));

        // Totals include at least this test's data (other tests share the database).
        Assert.True(report.Occupancy.ReservedHours >= 2m);
        Assert.True(report.Occupancy.CancelledCount >= 1);
        Assert.Contains(report.Occupancy.ByType, t => t.Type == WorkspaceType.HotDesk && t.ReservedHours >= 2m);
        Assert.DoesNotContain(report.Occupancy.ByType, t => t.Type == WorkspaceType.FixedDesk);
    }

    [Fact]
    public async Task Occupancy_WholeWeek_UsesTheOpeningHoursOfEachDay()
    {
        var room = await factory.SeedRoomAsync($"Week room {UniqueTag()}");
        var desk = await factory.SeedWorkspaceAsync(room.Id);
        var monday = Next(DayOfWeek.Monday);
        var saturday = monday.AddDays(5);
        await factory.SeedReservationAsync(desk.Id, monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await factory.SeedReservationAsync(desk.Id, saturday, new TimeOnly(9, 0), new TimeOnly(13, 0));

        // Monday–Sunday: 5 × 15 h + Saturday 8 h + Sunday 0 h = 83 h; reserved 8 + 4 = 12 h.
        var report = await GetReportAsync(monday, monday.AddDays(6));

        var row = Assert.Single(report.Occupancy.Workspaces, w => w.WorkspaceId == desk.Id);
        Assert.Equal((83m, 12m, 0.1446m, 2), (row.AvailableHours, row.ReservedHours, row.OccupancyRate, row.ReservationCount));
    }

    [Fact]
    public async Task Occupancy_ReservationOutsideThePeriod_IsNotCounted()
    {
        var room = await factory.SeedRoomAsync($"Outside room {UniqueTag()}");
        var desk = await factory.SeedWorkspaceAsync(room.Id);
        var day = NextWeekday();
        await factory.SeedReservationAsync(desk.Id, day, new TimeOnly(10, 0), new TimeOnly(12, 0));

        // The day before: the reservation of the next day doesn't touch it.
        var report = await GetReportAsync(day.AddDays(-1), day.AddDays(-1));

        var row = Assert.Single(report.Occupancy.Workspaces, w => w.WorkspaceId == desk.Id);
        Assert.Equal(0m, row.ReservedHours);
    }

    // ------------------------------------------------------------------ revenue

    [Fact]
    public async Task Revenue_PaymentInThePeriod_AppearsByTypeMethodAndDay()
    {
        var typeName = $"Report type {UniqueTag()}";
        var paidAt = DateTimeOffset.UtcNow.AddDays(-3);
        var paidOn = CenterTimeZone.ToLocalDate(paidAt);
        await SeedPaymentAsync(typeName, 99.50m, PaymentMethod.Card, paidAt);

        var report = await GetReportAsync(paidOn, paidOn);

        var type = Assert.Single(report.Revenue.ByMembershipType, t => t.Name == typeName);
        Assert.Equal((1, 99.50m), (type.PaymentCount, type.Total));
        Assert.True(report.Revenue.Total >= 99.50m);
        Assert.True(report.Revenue.PaymentCount >= 1);
        Assert.Contains(report.Revenue.ByMethod, m => m.Method == PaymentMethod.Card && m.Total >= 99.50m);
        Assert.Contains(report.Revenue.ByMethod, m => m.Method == PaymentMethod.Cash); // always both
        Assert.Contains(report.Revenue.ByDay, d => d.Date == paidOn && d.Total >= 99.50m);

        // A period that ends the day before the payment doesn't contain it.
        var before = await GetReportAsync(paidOn.AddDays(-7), paidOn.AddDays(-1));
        Assert.DoesNotContain(before.Revenue.ByMembershipType, t => t.Name == typeName);
    }

    // ------------------------------------------------------------------ validation

    [Theory]
    [InlineData("?to=2026-12-01")] // from missing
    [InlineData("?from=2026-12-01")] // to missing
    [InlineData("?from=2026-12-02&to=2026-12-01")] // end before start
    [InlineData("?from=2026-01-01&to=2027-01-02")] // 367 days
    public async Task InvalidPeriod_Returns400(string queryString)
    {
        using var manager = await factory.CreateClientAsAsync(Roles.Manager);

        var response = await manager.Client.GetAsync($"/api/reports/occupancy-revenue{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LongestPeriod_366Days_IsAllowed()
    {
        var report = await GetReportAsync(new DateOnly(2028, 1, 1), new DateOnly(2028, 12, 31)); // leap year

        Assert.Equal(new DateOnly(2028, 12, 31), report.To);
    }

    // ------------------------------------------------------------------ helpers

    private static DateOnly Today() => CenterTimeZone.ToLocalDate(DateTimeOffset.UtcNow);

    private static string ReportUrl(DateOnly from, DateOnly to) =>
        $"/api/reports/occupancy-revenue?from={Iso(from)}&to={Iso(to)}";

    private static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<OccupancyRevenueReport> GetReportAsync(DateOnly from, DateOnly to)
    {
        using var manager = await factory.CreateClientAsAsync(Roles.Manager);
        var response = await manager.Client.GetAsync(ReportUrl(from, to));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OccupancyRevenueReport>(Json))!;
    }

    /// <summary>A member who bought a membership of a NEW type and paid it at <paramref name="paidAt"/> (BR05).</summary>
    private async Task SeedPaymentAsync(string typeName, decimal price, PaymentMethod method, DateTimeOffset paidAt)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();

        var type = new MembershipType(typeName, 30, price);
        var member = new Member("Report", "Member", $"report-{Guid.NewGuid():N}@example.com", "+387 61 000 000", "hash", paidAt);
        var receptionist = new Employee(
            "Report",
            "Receptionist",
            $"report-receptionist-{Guid.NewGuid():N}@example.com",
            "hash",
            EmployeeRole.Receptionist
        );
        var membership = member.PurchaseMembership(type, paidAt);
        receptionist.RecordPayment(membership, membership.Price, method, paidAt);

        db.Members.Add(member);
        db.Employees.Add(receptionist);
        await db.SaveChangesAsync();
    }
}
