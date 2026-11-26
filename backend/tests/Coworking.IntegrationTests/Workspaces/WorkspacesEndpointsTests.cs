using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coworking.Application.Auth;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Workspaces;
using Coworking.Application.Workspaces.Dtos;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Coworking.IntegrationTests.Fixtures.TestSeed;

namespace Coworking.IntegrationTests.Workspaces;

/// <summary>
/// /api/workspaces end-to-end (FR09, UC11): CRUD, filters and paging, capacity per type
/// (validator AND database CHECK), unique codes, moving between rooms, the assigned-fixed-desk
/// guard, FK-protected delete, the JSON enum contract and the NFR01 access matrix.
/// </summary>
public class WorkspacesEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    /// <summary>The API sends and expects enums as names ("HotDesk"), so the test client must too.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // ------------------------------------------------------------------ access control (NFR01)

    /// <summary>Only the Manager reaches workspace management; roles don't inherit.</summary>
    public static TheoryData<string?, HttpStatusCode> AccessMatrix() =>
        new()
        {
            { null, HttpStatusCode.Unauthorized },
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
            using var anonymous = factory.CreateHttpsClient();
            response = await anonymous.GetAsync("/api/workspaces");
        }
        else
        {
            using var caller = await factory.CreateClientAsAsync(role);
            response = await caller.Client.GetAsync("/api/workspaces");
        }

        Assert.Equal(expected, response.StatusCode);
    }

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task Create_Returns201_WithNormalizedCode_AndRoomData()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync($"Offices {UniqueTag()}", floor: 2);
        var code = $"of-{UniqueTag()}";

        var response = await manager.PostAsJsonAsync(
            "/api/workspaces",
            new CreateWorkspaceRequest(room.Id, $"  {code} ", WorkspaceType.Office, 6),
            Json
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<WorkspaceResponse>(Json);
        Assert.EndsWith($"/api/workspaces/{created!.Id}", response.Headers.Location?.ToString(), StringComparison.Ordinal);
        Assert.Equal(code.ToUpperInvariant(), created.Code);
        Assert.Equal(WorkspaceType.Office, created.Type);
        Assert.Equal(6, created.Capacity);
        Assert.True(created.IsActive);
        Assert.Equal(room.Id, created.RoomId);
        Assert.Equal(room.Name, created.RoomName);
        Assert.True(created.RoomIsActive);
        Assert.False(created.IsAssigned);
    }

    [Fact]
    public async Task Create_TypeIsSentAndReturnedAsString()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());

        // Raw JSON, exactly like the frontend will send it.
        var response = await manager.PostAsync(
            "/api/workspaces",
            JsonContent.Create(new { roomId = room.Id, code = UniqueTag(), type = "MeetingRoom", capacity = 8 })
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MeetingRoom", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Create_TypeAsNumber_Returns400()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());

        // JSON contract (CW-13): integer enums are rejected.
        var response = await manager.PostAsync(
            "/api/workspaces",
            JsonContent.Create(new { roomId = room.Id, code = UniqueTag(), type = 1, capacity = 1 })
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(WorkspaceType.HotDesk, 2)]
    [InlineData(WorkspaceType.FixedDesk, 0)]
    [InlineData(WorkspaceType.Office, 21)]
    [InlineData(WorkspaceType.MeetingRoom, 1)]
    public async Task Create_CapacityThatDoesNotFitTheType_Returns400(WorkspaceType type, int capacity)
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());

        var response = await manager.PostAsJsonAsync(
            "/api/workspaces",
            new CreateWorkspaceRequest(room.Id, UniqueTag(), type, capacity),
            Json
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("capacity", out _));
    }

    [Theory]
    [InlineData("-A1")] // must not start with "-"
    [InlineData("A 1")] // no spaces inside
    [InlineData("Š1")] // only A–Z
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")] // 21 characters
    public async Task Create_InvalidCode_Returns400(string code)
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());

        var response = await manager.PostAsJsonAsync(
            "/api/workspaces",
            new CreateWorkspaceRequest(room.Id, code, WorkspaceType.HotDesk, 1),
            Json
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("code", out _));
    }

    [Fact]
    public async Task Create_DuplicateCodeInOtherCase_Returns409CodeTaken()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var existing = await factory.SeedWorkspaceAsync(room.Id);

        var response = await manager.PostAsJsonAsync(
            "/api/workspaces",
            new CreateWorkspaceRequest(room.Id, existing.Code.ToLowerInvariant(), WorkspaceType.HotDesk, 1),
            Json
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(WorkspaceErrorCodes.CodeTaken, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Create_UnknownRoom_Returns404()
    {
        var manager = await factory.CreateManagerClientAsync();

        var response = await manager.PostAsJsonAsync(
            "/api/workspaces",
            new CreateWorkspaceRequest(999999, UniqueTag(), WorkspaceType.HotDesk, 1),
            Json
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_InDeactivatedRoom_Returns422RoomInactive()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag(), isActive: false);

        var response = await manager.PostAsJsonAsync(
            "/api/workspaces",
            new CreateWorkspaceRequest(room.Id, UniqueTag(), WorkspaceType.HotDesk, 1),
            Json
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(WorkspaceErrorCodes.RoomInactive, await ReadCodeAsync(response));
    }

    // ------------------------------------------------------------------ list, get

    [Fact]
    public async Task List_FiltersByRoomTypeAndStatus_SortedByCode()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var tag = UniqueTag();
        await factory.SeedWorkspaceAsync(room.Id, code: $"{tag}-B");
        await factory.SeedWorkspaceAsync(room.Id, code: $"{tag}-A");
        await factory.SeedWorkspaceAsync(room.Id, code: $"{tag}-C", isActive: false);
        await factory.SeedWorkspaceAsync(room.Id, WorkspaceType.Office, 4, code: $"{tag}-O");

        var result = await manager.GetFromJsonAsync<PagedResult<WorkspaceResponse>>(
            $"/api/workspaces?roomId={room.Id}&type=HotDesk&isActive=true",
            Json
        );

        Assert.Equal(
            new[] { $"{tag}-A", $"{tag}-B" }.Select(c => c.ToUpperInvariant()),
            result!.Items.Select(w => w.Code)
        );
    }

    [Fact]
    public async Task List_SearchByCode_IgnoresCase_AndPages()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var tag = UniqueTag();
        for (var i = 1; i <= 3; i++)
        {
            await factory.SeedWorkspaceAsync(room.Id, code: $"{tag}-{i}");
        }

        var page2 = await manager.GetFromJsonAsync<PagedResult<WorkspaceResponse>>(
            $"/api/workspaces?search={tag}&pageSize=2&page=2",
            Json
        );

        Assert.Equal(3, page2!.TotalCount);
        Assert.Equal($"{tag}-3".ToUpperInvariant(), Assert.Single(page2.Items).Code);
    }

    [Fact]
    public async Task List_UnknownTypeName_Returns400()
    {
        var manager = await factory.CreateManagerClientAsync();

        var response = await manager.GetAsync("/api/workspaces?type=Sofa");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ShowsRoomStatus_AndAssignment()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var desk = await factory.SeedWorkspaceAsync(room.Id, WorkspaceType.FixedDesk);
        await factory.AssignFixedDeskAsync(desk.Id);
        await manager.PostAsync($"/api/rooms/{room.Id}/deactivate", null);

        var result = await manager.GetFromJsonAsync<WorkspaceResponse>($"/api/workspaces/{desk.Id}", Json);

        Assert.True(result!.IsActive); // own flag untouched by the room (CW-18)
        Assert.False(result.RoomIsActive);
        Assert.True(result.IsAssigned);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var manager = await factory.CreateManagerClientAsync();

        var response = await manager.GetAsync("/api/workspaces/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public async Task Update_ChangesCodeTypeCapacity_AndMovesToAnotherRoom()
    {
        var manager = await factory.CreateManagerClientAsync();
        var from = await factory.SeedRoomAsync(UniqueTag());
        var to = await factory.SeedRoomAsync(UniqueTag());
        var workspace = await factory.SeedWorkspaceAsync(from.Id);
        var newCode = UniqueTag();

        var response = await manager.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}",
            new UpdateWorkspaceRequest(to.Id, newCode, WorkspaceType.MeetingRoom, 10),
            Json
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<WorkspaceResponse>(Json);
        Assert.Equal(newCode.ToUpperInvariant(), updated!.Code);
        Assert.Equal(WorkspaceType.MeetingRoom, updated.Type);
        Assert.Equal(10, updated.Capacity);
        Assert.Equal(to.Id, updated.RoomId);
        Assert.Equal(to.Name, updated.RoomName);
    }

    [Fact]
    public async Task Update_KeepingItsOwnCode_IsAllowed()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var workspace = await factory.SeedWorkspaceAsync(room.Id, WorkspaceType.Office, 4);

        var response = await manager.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}",
            new UpdateWorkspaceRequest(room.Id, workspace.Code.ToLowerInvariant(), WorkspaceType.Office, 6),
            Json
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_CodeOfAnotherWorkspace_Returns409CodeTaken()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var other = await factory.SeedWorkspaceAsync(room.Id);
        var workspace = await factory.SeedWorkspaceAsync(room.Id);

        var response = await manager.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}",
            new UpdateWorkspaceRequest(room.Id, other.Code, WorkspaceType.HotDesk, 1),
            Json
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(WorkspaceErrorCodes.CodeTaken, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Update_MoveIntoDeactivatedRoom_Returns422RoomInactive()
    {
        var manager = await factory.CreateManagerClientAsync();
        var from = await factory.SeedRoomAsync(UniqueTag());
        var closed = await factory.SeedRoomAsync(UniqueTag(), isActive: false);
        var workspace = await factory.SeedWorkspaceAsync(from.Id);

        var response = await manager.PutAsJsonAsync(
            $"/api/workspaces/{workspace.Id}",
            new UpdateWorkspaceRequest(closed.Id, workspace.Code, WorkspaceType.HotDesk, 1),
            Json
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(WorkspaceErrorCodes.RoomInactive, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Update_AssignedFixedDesk_CantChangeType_ButCanBeRenamed()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var desk = await factory.SeedWorkspaceAsync(room.Id, WorkspaceType.FixedDesk);
        await factory.AssignFixedDeskAsync(desk.Id);

        var retype = await manager.PutAsJsonAsync(
            $"/api/workspaces/{desk.Id}",
            new UpdateWorkspaceRequest(room.Id, desk.Code, WorkspaceType.HotDesk, 1),
            Json
        );
        var rename = await manager.PutAsJsonAsync(
            $"/api/workspaces/{desk.Id}",
            new UpdateWorkspaceRequest(room.Id, UniqueTag(), WorkspaceType.FixedDesk, 1),
            Json
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, retype.StatusCode);
        Assert.Equal(WorkspaceErrorCodes.FixedAssigned, await ReadCodeAsync(retype));
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    }

    // ------------------------------------------------------------------ deactivate, activate

    [Fact]
    public async Task DeactivateThenActivate_Returns204_AndChangesOnlyTheWorkspace()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var workspace = await factory.SeedWorkspaceAsync(room.Id);

        var deactivate = await manager.PostAsync($"/api/workspaces/{workspace.Id}/deactivate", null);
        var again = await manager.PostAsync($"/api/workspaces/{workspace.Id}/deactivate", null);
        var afterDeactivate = await manager.GetFromJsonAsync<WorkspaceResponse>($"/api/workspaces/{workspace.Id}", Json);
        var activate = await manager.PostAsync($"/api/workspaces/{workspace.Id}/activate", null);
        var afterActivate = await manager.GetFromJsonAsync<WorkspaceResponse>($"/api/workspaces/{workspace.Id}", Json);

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode); // idempotent
        Assert.False(afterDeactivate!.IsActive);
        Assert.True(afterDeactivate.RoomIsActive);
        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        Assert.True(afterActivate!.IsActive);
    }

    [Fact]
    public async Task Deactivate_AssignedFixedDesk_Returns422FixedAssigned()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var desk = await factory.SeedWorkspaceAsync(room.Id, WorkspaceType.FixedDesk);
        await factory.AssignFixedDeskAsync(desk.Id);

        var response = await manager.PostAsync($"/api/workspaces/{desk.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(WorkspaceErrorCodes.FixedAssigned, await ReadCodeAsync(response));
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task Delete_NeverReserved_Returns204_AndRoomCanThenBeDeleted()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var workspace = await factory.SeedWorkspaceAsync(room.Id);

        var delete = await manager.DeleteAsync($"/api/workspaces/{workspace.Id}");
        var get = await manager.GetAsync($"/api/workspaces/{workspace.Id}");
        var deleteRoom = await manager.DeleteAsync($"/api/rooms/{room.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteRoom.StatusCode); // room is empty now
    }

    [Fact]
    public async Task Delete_WithReservation_Returns409InUse()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var workspace = await factory.SeedWorkspaceAsync(room.Id);
        await factory.SeedReservationAsync(workspace.Id);

        var response = await manager.DeleteAsync($"/api/workspaces/{workspace.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("conflict.in_use", await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Delete_AssignedFixedDesk_Returns422_AndTheMemberKeepsTheDesk()
    {
        var manager = await factory.CreateManagerClientAsync();
        var room = await factory.SeedRoomAsync(UniqueTag());
        var desk = await factory.SeedWorkspaceAsync(room.Id, WorkspaceType.FixedDesk);
        await factory.AssignFixedDeskAsync(desk.Id);

        var response = await manager.DeleteAsync($"/api/workspaces/{desk.Id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(WorkspaceErrorCodes.FixedAssigned, await ReadCodeAsync(response));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        Assert.True(await db.Members.AnyAsync(m => m.FixedWorkspaceId == desk.Id));
    }

    // ------------------------------------------------------------------ database backstop

    [Fact]
    public async Task Database_RejectsCapacityThatDoesNotFitTheType()
    {
        // Bypasses the validator and the domain with raw SQL: the CHECK constraint
        // ck_workspaces_capacity_by_type is the last line of defence (SqlState 23514).
        var room = await factory.SeedRoomAsync(UniqueTag());
        var code = UniqueTag().ToUpperInvariant();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO workspaces (code, type, capacity, is_active, room_id) VALUES ({code}, 'HotDesk', 3, true, {room.Id})"
            )
        );

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_workspaces_capacity_by_type", exception.ConstraintName);
    }
}
