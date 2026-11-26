using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Coworking.Application.Auth;
using Coworking.Application.Common.Abstractions;
using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coworking.IntegrationTests.Fixtures;

/// <summary>
/// Builders that put data straight into the test database (no API calls), plus small client
/// helpers. Shared by the Rooms, Workspaces, MembershipTypes and Availability tests (CW-18–21) and meant for Sprint 4/5 too.
/// </summary>
/// <remarks>
/// Every test class shares ONE database, so everything created here gets unique names, codes and
/// e-mails (<see cref="UniqueTag"/>). Each method uses its own DbContext scope: the returned
/// entities are detached snapshots – use their ids, don't modify them.
/// </remarks>
public static class TestSeed
{
    /// <summary>Password hash placeholder for seeded users that never log in through the API.</summary>
    private const string UnusedHash = "hash";

    /// <summary>"t" + 10 hex characters, e.g. "t3f9a01bc2d". Short enough to be part of a workspace code.</summary>
    public static string UniqueTag() => "t" + Guid.NewGuid().ToString("N")[..10];

    /// <summary>A client for https://localhost without cookie handling (Secure cookies, like the other tests).</summary>
    /// <param name="factory">The API factory.</param>
    public static HttpClient CreateHttpsClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false }
        );

    /// <summary>
    /// A client acting as a fresh Manager. The token comes straight from the app's token service,
    /// which is faster than logging in (the login flow has its own tests).
    /// </summary>
    /// <param name="factory">A factory with a database.</param>
    public static async Task<HttpClient> CreateManagerClientAsync(this PostgresApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var manager = new Employee("Test", "Manager", UniqueEmail("manager"), UnusedHash, EmployeeRole.Manager);
        db.Employees.Add(manager);
        await db.SaveChangesAsync();

        var token = factory.Services.GetRequiredService<IAccessTokenService>().Create(AuthUser.FromEmployee(manager));
        var client = factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    /// <summary>
    /// Inserts a room, optionally with hot desks. The first <paramref name="inactiveWorkspaces"/>
    /// desks are deactivated.
    /// </summary>
    /// <param name="factory">A factory with a database.</param>
    /// <param name="name">Room name (unique; use <see cref="UniqueTag"/>).</param>
    /// <param name="floor">Floor.</param>
    /// <param name="isActive">false = the room is deactivated after its workspaces were added.</param>
    /// <param name="workspaces">Number of hot desks to add.</param>
    /// <param name="inactiveWorkspaces">How many of those desks are deactivated.</param>
    public static async Task<Room> SeedRoomAsync(
        this PostgresApiFactory factory,
        string name,
        int floor = 1,
        bool isActive = true,
        int workspaces = 0,
        int inactiveWorkspaces = 0
    )
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();

        var room = new Room(name, floor, null);
        for (var i = 0; i < workspaces; i++)
        {
            var desk = room.AddWorkspace($"{UniqueTag()}-{i}", WorkspaceType.HotDesk, 1);
            if (i < inactiveWorkspaces)
            {
                desk.Deactivate();
            }
        }

        // Deactivate AFTER adding workspaces: AddWorkspace refuses inactive rooms.
        if (!isActive)
        {
            room.Deactivate();
        }

        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room;
    }

    /// <summary>Inserts one workspace into an existing room (which must be active).</summary>
    /// <param name="factory">A factory with a database.</param>
    /// <param name="roomId">The room.</param>
    /// <param name="type">Workspace type.</param>
    /// <param name="capacity">Capacity that fits the type.</param>
    /// <param name="isActive">false = deactivated right away.</param>
    /// <param name="code">Code; a unique one is generated when null.</param>
    public static async Task<Workspace> SeedWorkspaceAsync(
        this PostgresApiFactory factory,
        int roomId,
        WorkspaceType type = WorkspaceType.HotDesk,
        int capacity = 1,
        bool isActive = true,
        string? code = null
    )
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();

        var room = await db.Rooms.SingleAsync(r => r.Id == roomId);
        var workspace = room.AddWorkspace(code ?? UniqueTag(), type, capacity);
        if (!isActive)
        {
            workspace.Deactivate();
        }

        await db.SaveChangesAsync();
        return workspace;
    }

    /// <summary>Creates a member and assigns the fixed desk to them (members.fixed_workspace_id).</summary>
    /// <param name="factory">A factory with a database.</param>
    /// <param name="workspaceId">A workspace of type FixedDesk.</param>
    public static async Task AssignFixedDeskAsync(this PostgresApiFactory factory, int workspaceId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();

        var desk = await db.Workspaces.SingleAsync(w => w.Id == workspaceId);
        var member = NewMember(DateTimeOffset.UtcNow);
        member.AssignFixedWorkspace(desk);

        db.Members.Add(member);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates a member with a PAID monthly membership (30 days from today) and a reservation of the
    /// workspace. Without arguments: the next weekday at least two days ahead, 10:00–12:00 Sarajevo
    /// time (inside working hours, BR04; inside the membership, BR02).
    /// </summary>
    /// <param name="factory">A factory with a database.</param>
    /// <param name="workspaceId">The workspace to reserve.</param>
    /// <param name="day">Local date; must be in the future and within 30 days (the membership).</param>
    /// <param name="from">Local start time (default 10:00).</param>
    /// <param name="to">Local end time (default 12:00).</param>
    /// <param name="cancelled">true = the member cancels it right away (status Cancelled, CW-21).</param>
    /// <returns>The reservation (detached).</returns>
    public static async Task<Reservation> SeedReservationAsync(
        this PostgresApiFactory factory,
        int workspaceId,
        DateOnly? day = null,
        TimeOnly? from = null,
        TimeOnly? to = null,
        bool cancelled = false
    )
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var now = DateTimeOffset.UtcNow;
        var date = day ?? NextWeekday(minDaysAhead: 2);

        var workspace = await db.Workspaces.SingleAsync(w => w.Id == workspaceId);
        var receptionist = new Employee("Test", "Receptionist", UniqueEmail("receptionist"), UnusedHash, EmployeeRole.Receptionist);
        var member = NewMember(now);

        // BR05: the membership becomes active only through a recorded payment.
        var membership = member.PurchaseMembership(new MembershipType($"Monthly {UniqueTag()}", 30, 120m), now);
        receptionist.RecordPayment(membership, membership.Price, PaymentMethod.Cash, now);

        var reservation = Reservation.Create(
            member,
            workspace,
            CenterTimeZone.FromLocal(date, from ?? new TimeOnly(10, 0)),
            CenterTimeZone.FromLocal(date, to ?? new TimeOnly(12, 0)),
            now
        );

        if (cancelled)
        {
            // Days ahead, so the member is still allowed to cancel (BR03: at least 2 h before).
            reservation.CancelByMember(now);
        }

        db.Employees.Add(receptionist);
        db.Members.Add(member);
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation;
    }

    /// <summary>
    /// The first Monday–Friday that is at least <paramref name="minDaysAhead"/> days after today
    /// (Sarajevo date). At least 2 days avoids any UTC/local edge around midnight.
    /// </summary>
    /// <param name="minDaysAhead">Minimum distance from today.</param>
    public static DateOnly NextWeekday(int minDaysAhead = 2)
    {
        var day = CenterTimeZone.ToLocalDate(DateTimeOffset.UtcNow).AddDays(minDaysAhead);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            day = day.AddDays(1);
        }

        return day;
    }

    /// <summary>The first given day of the week at least <paramref name="minDaysAhead"/> days after today.</summary>
    /// <param name="dayOfWeek">E.g. Saturday.</param>
    /// <param name="minDaysAhead">Minimum distance from today.</param>
    public static DateOnly Next(DayOfWeek dayOfWeek, int minDaysAhead = 2)
    {
        var day = CenterTimeZone.ToLocalDate(DateTimeOffset.UtcNow).AddDays(minDaysAhead);
        while (day.DayOfWeek != dayOfWeek)
        {
            day = day.AddDays(1);
        }

        return day;
    }

    /// <summary>Inserts a membership type (CW-20).</summary>
    /// <param name="factory">A factory with a database.</param>
    /// <param name="name">Name (unique; use <see cref="UniqueTag"/>).</param>
    /// <param name="durationDays">Duration in days.</param>
    /// <param name="price">Price in KM.</param>
    /// <param name="isActive">false = deactivated right away.</param>
    public static async Task<MembershipType> SeedMembershipTypeAsync(
        this PostgresApiFactory factory,
        string name,
        int durationDays = 30,
        decimal price = 120m,
        bool isActive = true
    )
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();

        var type = new MembershipType(name, durationDays, price);
        if (!isActive)
        {
            type.Deactivate();
        }

        db.MembershipTypes.Add(type);
        await db.SaveChangesAsync();
        return type;
    }

    /// <summary>
    /// A new member buys a membership of the given (active) type – Unpaid, like UC03 step 4.
    /// The membership copies the type's current price and duration.
    /// </summary>
    /// <param name="factory">A factory with a database.</param>
    /// <param name="membershipTypeId">The type to buy.</param>
    /// <returns>Id of the new membership.</returns>
    public static async Task<int> SeedMembershipAsync(this PostgresApiFactory factory, int membershipTypeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var now = DateTimeOffset.UtcNow;

        var type = await db.MembershipTypes.SingleAsync(t => t.Id == membershipTypeId);
        var member = NewMember(now);
        var membership = member.PurchaseMembership(type, now);

        db.Members.Add(member);
        await db.SaveChangesAsync();
        return membership.Id;
    }

    /// <summary>The <c>code</c> field of a ProblemDetails response.</summary>
    /// <param name="response">An error response.</param>
    public static async Task<string?> ReadCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.com";

    private static Member NewMember(DateTimeOffset now) =>
        new("Test", "Member", UniqueEmail("member"), "+387 61 000 000", UnusedHash, now);
}
