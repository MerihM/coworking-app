using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Coworking.Application.Auth;
using Coworking.Application.Common.Abstractions;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Rooms;
using Coworking.Application.Rooms.Dtos;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Coworking.IntegrationTests.Rooms;

/// <summary>
/// /api/rooms end-to-end (FR09, UC11): CRUD, paging and search, case-insensitive unique names
/// (service check AND database index), deactivation that leaves workspace flags alone,
/// FK-protected delete, and the NFR01 access matrix.
/// </summary>
/// <remarks>
/// The database is shared by the whole class, so every test uses its own unique names/codes
/// (<see cref="UniqueTag"/>) and never assumes how many rooms exist in total.
/// </remarks>
public class RoomsEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    // ------------------------------------------------------------------ access control (NFR01)

    /// <summary>Only the Manager reaches room management; roles don't inherit.</summary>
    public static TheoryData<string?, HttpStatusCode> AccessMatrix() =>
        new()
        {
            { null, HttpStatusCode.Unauthorized }, // anonymous
            { Roles.Member, HttpStatusCode.Forbidden },
            { Roles.Receptionist, HttpStatusCode.Forbidden },
            { Roles.Administrator, HttpStatusCode.Forbidden },
            { Roles.Manager, HttpStatusCode.OK },
        };

    [Theory]
    [MemberData(nameof(AccessMatrix))]
    public async Task NFR01_List_OnlyManagerHasAccess(string? role, HttpStatusCode expected)
    {
        HttpResponseMessage response;
        if (role is null)
        {
            using var anonymous = CreateClient();
            response = await anonymous.GetAsync("/api/rooms");
        }
        else
        {
            // Real login flow (creates a user with this role and logs in through /api/auth/login).
            using var caller = await factory.CreateClientAsAsync(role);
            response = await caller.Client.GetAsync("/api/rooms");
        }

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task NFR01_Create_AsAdministrator_Returns403()
    {
        using var admin = await factory.CreateClientAsAsync(Roles.Administrator);

        var response = await admin.Client.PostAsJsonAsync("/api/rooms", new CreateRoomRequest(UniqueTag(), 1, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task Create_Returns201_WithLocation_AndTrimmedData()
    {
        var manager = await ManagerClientAsync();
        var name = $"Lab {UniqueTag()}";

        var response = await manager.PostAsJsonAsync(
            "/api/rooms",
            new CreateRoomRequest($"  {name}  ", 3, "   ")
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.EndsWith($"/api/rooms/{created!.Id}", response.Headers.Location?.ToString(), StringComparison.Ordinal);
        Assert.Equal(name, created.Name);
        Assert.Equal(3, created.Floor);
        Assert.Null(created.Description);
        Assert.True(created.IsActive);
        Assert.Equal(0, created.WorkspaceCount);
    }

    [Fact]
    public async Task Create_SameNameInOtherLetterCase_Returns409NameTaken()
    {
        var manager = await ManagerClientAsync();
        var name = $"Studio {UniqueTag()}";
        await SeedRoomAsync(name);

        var response = await manager.PostAsJsonAsync(
            "/api/rooms",
            new CreateRoomRequest($" {name.ToUpperInvariant()} ", 1, null)
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(RoomErrorCodes.NameTaken, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Create_InvalidInput_Returns400_WithCamelCaseFieldErrors()
    {
        var manager = await ManagerClientAsync();

        var response = await manager.PostAsJsonAsync(
            "/api/rooms",
            new CreateRoomRequest(" A ", 101, new string('x', 1001))
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("floor", out _));
        Assert.True(errors.TryGetProperty("description", out _));
    }

    [Fact]
    public async Task Create_FloorAsString_Returns400()
    {
        var manager = await ManagerClientAsync();

        // JSON contract (CW-13): numbers must be JSON numbers, "2" is rejected.
        var response = await manager.PostAsync(
            "/api/rooms",
            JsonContent.Create(new { name = UniqueTag(), floor = "2", description = (string?)null })
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_BosnianLanguage_ReturnsBosnianMessage()
    {
        var manager = await ManagerClientAsync();
        var name = $"Soba {UniqueTag()}";
        await SeedRoomAsync(name);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rooms")
        {
            Content = JsonContent.Create(new CreateRoomRequest(name, 1, null)),
        };
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("bs"));
        var response = await manager.SendAsync(request);

        var detail = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
        Assert.Equal("Prostorija s ovim nazivom već postoji.", detail);
    }

    // ------------------------------------------------------------------ list, get

    [Fact]
    public async Task List_SearchAndPaging_SortedByFloorThenName()
    {
        var manager = await ManagerClientAsync();
        var tag = UniqueTag();
        await SeedRoomAsync($"{tag} Beta", floor: 2);
        await SeedRoomAsync($"{tag} Alpha", floor: 2);
        await SeedRoomAsync($"{tag} Zulu", floor: 1);

        var page1 = await manager.GetFromJsonAsync<PagedResult<RoomResponse>>(
            $"/api/rooms?search={tag.ToUpperInvariant()}&pageSize=2&page=1"
        );
        var page2 = await manager.GetFromJsonAsync<PagedResult<RoomResponse>>(
            $"/api/rooms?search={tag}&pageSize=2&page=2"
        );

        Assert.Equal(3, page1!.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(new[] { $"{tag} Zulu", $"{tag} Alpha" }, page1.Items.Select(r => r.Name));
        Assert.Equal(new[] { $"{tag} Beta" }, page2!.Items.Select(r => r.Name));
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        var manager = await ManagerClientAsync();
        var tag = UniqueTag();
        await SeedRoomAsync($"{tag} on");
        await SeedRoomAsync($"{tag} off", isActive: false);

        var result = await manager.GetFromJsonAsync<PagedResult<RoomResponse>>(
            $"/api/rooms?search={tag}&isActive=false"
        );

        var only = Assert.Single(result!.Items);
        Assert.Equal($"{tag} off", only.Name);
        Assert.False(only.IsActive);
    }

    [Fact]
    public async Task Get_ReturnsWorkspaceCounts()
    {
        var manager = await ManagerClientAsync();
        var room = await SeedRoomAsync(UniqueTag(), workspaces: 3, inactiveWorkspaces: 1);

        var result = await manager.GetFromJsonAsync<RoomResponse>($"/api/rooms/{room.Id}");

        Assert.Equal(3, result!.WorkspaceCount);
        Assert.Equal(2, result.ActiveWorkspaceCount);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var manager = await ManagerClientAsync();

        var response = await manager.GetAsync("/api/rooms/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public async Task Update_ChangesAllFields_AndKeepsCounts()
    {
        var manager = await ManagerClientAsync();
        var room = await SeedRoomAsync(UniqueTag(), workspaces: 2);
        var newName = $"Renamed {UniqueTag()}";

        var response = await manager.PutAsJsonAsync(
            $"/api/rooms/{room.Id}",
            new UpdateRoomRequest(newName, -1, "Basement")
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal(newName, updated!.Name);
        Assert.Equal(-1, updated.Floor);
        Assert.Equal("Basement", updated.Description);
        Assert.Equal(2, updated.WorkspaceCount);
    }

    [Fact]
    public async Task Update_OwnNameInOtherCase_IsAllowed()
    {
        var manager = await ManagerClientAsync();
        var name = $"Corner {UniqueTag()}";
        var room = await SeedRoomAsync(name);

        var response = await manager.PutAsJsonAsync(
            $"/api/rooms/{room.Id}",
            new UpdateRoomRequest(name.ToUpperInvariant(), 1, null)
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_NameOfAnotherRoom_Returns409NameTaken()
    {
        var manager = await ManagerClientAsync();
        var taken = await SeedRoomAsync($"Taken {UniqueTag()}");
        var room = await SeedRoomAsync(UniqueTag());

        var response = await manager.PutAsJsonAsync(
            $"/api/rooms/{room.Id}",
            new UpdateRoomRequest(taken.Name, 1, null)
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(RoomErrorCodes.NameTaken, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Update_UnknownId_Returns404()
    {
        var manager = await ManagerClientAsync();

        var response = await manager.PutAsJsonAsync(
            "/api/rooms/999999",
            new UpdateRoomRequest(UniqueTag(), 1, null)
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ deactivate, activate

    [Fact]
    public async Task Deactivate_HidesTheRoom_ButKeepsWorkspaceFlags_ThenActivateRestores()
    {
        var manager = await ManagerClientAsync();
        var room = await SeedRoomAsync(UniqueTag(), workspaces: 2, inactiveWorkspaces: 1);

        var deactivate = await manager.PostAsync($"/api/rooms/{room.Id}/deactivate", null);
        var afterDeactivate = await manager.GetFromJsonAsync<RoomResponse>($"/api/rooms/{room.Id}");
        var activate = await manager.PostAsync($"/api/rooms/{room.Id}/activate", null);
        var afterActivate = await manager.GetFromJsonAsync<RoomResponse>($"/api/rooms/{room.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.False(afterDeactivate!.IsActive);
        Assert.Equal(1, afterDeactivate.ActiveWorkspaceCount); // workspace flags untouched

        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        Assert.True(afterActivate!.IsActive);
        Assert.Equal(1, afterActivate.ActiveWorkspaceCount);
    }

    [Fact]
    public async Task Deactivate_Twice_Returns204BothTimes()
    {
        var manager = await ManagerClientAsync();
        var room = await SeedRoomAsync(UniqueTag());

        var first = await manager.PostAsync($"/api/rooms/{room.Id}/deactivate", null);
        var second = await manager.PostAsync($"/api/rooms/{room.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task Deactivate_UnknownId_Returns404()
    {
        var manager = await ManagerClientAsync();

        var response = await manager.PostAsync("/api/rooms/999999/deactivate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task Delete_EmptyRoom_Returns204_ThenGetReturns404()
    {
        var manager = await ManagerClientAsync();
        var room = await SeedRoomAsync(UniqueTag());

        var delete = await manager.DeleteAsync($"/api/rooms/{room.Id}");
        var get = await manager.GetAsync($"/api/rooms/{room.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Delete_RoomWithWorkspaces_Returns409InUse_AndRoomStays()
    {
        var manager = await ManagerClientAsync();
        var room = await SeedRoomAsync(UniqueTag(), workspaces: 1);

        var response = await manager.DeleteAsync($"/api/rooms/{room.Id}");
        var get = await manager.GetAsync($"/api/rooms/{room.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("conflict.in_use", await ReadCodeAsync(response));
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        var manager = await ManagerClientAsync();

        var response = await manager.DeleteAsync("/api/rooms/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ database backstop

    [Fact]
    public async Task Database_RejectsSameNameInOtherCase_EvenWithoutTheServiceCheck()
    {
        // Simulates two requests racing past the service check: both rows reach the database.
        // The expression index ux_rooms_name_lower must stop the second one (SqlState 23505).
        var name = $"Race {UniqueTag()}";
        await SeedRoomAsync(name);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        db.Rooms.Add(new Room(name.ToUpperInvariant(), 1, null));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("ux_rooms_name_lower", postgres.ConstraintName);
    }

    // ------------------------------------------------------------------ helpers

    private HttpClient CreateClient() =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false }
        );

    private static string UniqueTag() => "t" + Guid.NewGuid().ToString("N")[..10];

    /// <summary>A client acting as a fresh Manager. The token comes straight from the app's token service (faster than logging in).</summary>
    private async Task<HttpClient> ManagerClientAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var manager = new Employee("Test", "Manager", $"manager-{Guid.NewGuid():N}@example.com", "hash", EmployeeRole.Manager);
        db.Employees.Add(manager);
        await db.SaveChangesAsync();

        var token = factory.Services.GetRequiredService<IAccessTokenService>().Create(AuthUser.FromEmployee(manager));
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    /// <summary>
    /// Inserts a room directly (no API call), optionally with hot desks. The first
    /// <paramref name="inactiveWorkspaces"/> desks are deactivated.
    /// </summary>
    private async Task<Room> SeedRoomAsync(
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
            // Codes are unique in the whole center: max 20 chars, so 1 + 10 + "-" + digits fits.
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

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}
