using System.Net;
using System.Net.Http.Json;
using Coworking.Application.Auth;
using Coworking.Application.Auth.Dtos;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Members;
using Coworking.Application.Members.Dtos;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.IntegrationTests.Fixtures;
using static Coworking.IntegrationTests.Fixtures.TestSeed;

namespace Coworking.IntegrationTests.Members;

/// <summary>
/// /api/members end-to-end (FR08, UC10): create with an initial password, e-mail unique across
/// members AND employees, search, edit with the fixed desk rules, deactivation blocks login,
/// FK-protected delete, and the NFR01 access matrix.
/// </summary>
public class MembersEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    // ------------------------------------------------------------------ access control (NFR01)

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(Roles.Member, HttpStatusCode.Forbidden)]
    [InlineData(Roles.Receptionist, HttpStatusCode.OK)]
    [InlineData(Roles.Manager, HttpStatusCode.OK)]
    [InlineData(Roles.Administrator, HttpStatusCode.Forbidden)]
    public async Task NFR01_OnlyReceptionAndManagerManageMembers(string? role, HttpStatusCode expected)
    {
        HttpResponseMessage response;
        if (role is null)
        {
            using var anonymous = factory.CreateHttpsClient();
            response = await anonymous.GetAsync("/api/members");
        }
        else
        {
            using var caller = await factory.CreateClientAsAsync(role);
            response = await caller.Client.GetAsync("/api/members");
        }

        Assert.Equal(expected, response.StatusCode);
    }

    // ------------------------------------------------------------------ create

    [Fact]
    public async Task Create_Returns201_AndTheMemberCanLogIn()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var email = $"Desk-{UniqueTag()}@Example.com";

        var response = await reception.Client.PostAsJsonAsync("/api/members", NewMember(email));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<MemberResponse>())!;
        Assert.Equal(email.ToLowerInvariant(), created.Email);
        Assert.True(created.IsActive);
        Assert.Null(created.CurrentMembership);
        Assert.False(created.HasUnpaidMembership);
        Assert.EndsWith($"/api/members/{created.Id}", response.Headers.Location?.ToString(), StringComparison.Ordinal);

        using var anonymous = factory.CreateHttpsClient();
        var login = await anonymous.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, AuthenticatedClientFactory.Password)
        );
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Create_WithAnEmployeesEmail_Returns409EmailTaken()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);

        // The receptionist's own e-mail: unique across BOTH tables, not only members.
        var response = await reception.Client.PostAsJsonAsync("/api/members", NewMember(reception.User.Email));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AuthErrorCodes.EmailTaken, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Create_InvalidPhoneAndPassword_Returns400WithCamelCaseKeys()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);

        var response = await reception.Client.PostAsJsonAsync(
            "/api/members",
            NewMember($"{UniqueTag()}@example.com") with { Phone = "abc", Password = "short" }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"phone\"", body, StringComparison.Ordinal);
        Assert.Contains("\"password\"", body, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ list

    [Fact]
    public async Task List_SearchFindsByLastNameAndByPhone()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var lastName = $"Zz{UniqueTag()}";
        var phone = $"+387 {Random.Shared.Next(100_000, 999_999)} 01";
        await CreateAsync(reception.Client, NewMember($"{UniqueTag()}@example.com") with { LastName = lastName, Phone = phone });

        var byName = await reception.Client.GetFromJsonAsync<PagedResult<MemberResponse>>(
            $"/api/members?search={lastName.ToUpperInvariant()}"
        );
        var byPhone = await reception.Client.GetFromJsonAsync<PagedResult<MemberResponse>>(
            $"/api/members?search={Uri.EscapeDataString(phone)}"
        );

        Assert.Equal(lastName, Assert.Single(byName!.Items).LastName);
        Assert.Equal(phone, Assert.Single(byPhone!.Items).Phone);
    }

    // ------------------------------------------------------------------ update + fixed desk

    [Fact]
    public async Task Update_ChangesDetails_AndAssignsThenRemovesAFixedDesk()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var member = await CreateAsync(reception.Client, NewMember($"{UniqueTag()}@example.com"));
        var desk = await SeedDeskAsync(WorkspaceType.FixedDesk);
        var newEmail = $"{UniqueTag()}@example.com";

        var assigned = await reception.Client.PutAsJsonAsync(
            $"/api/members/{member.Id}",
            new UpdateMemberRequest("Lejla", "Novak", newEmail, "061/222-333", desk.Id)
        );
        var assignedBody = (await assigned.Content.ReadFromJsonAsync<MemberResponse>())!;

        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        Assert.Equal(("Lejla", "Novak", newEmail, "061/222-333"), (assignedBody.FirstName, assignedBody.LastName, assignedBody.Email, assignedBody.Phone));
        Assert.Equal(desk.Id, assignedBody.FixedDesk?.Id);

        var removed = await reception.Client.PutAsJsonAsync(
            $"/api/members/{member.Id}",
            new UpdateMemberRequest("Lejla", "Novak", newEmail, "061/222-333", null)
        );
        Assert.Null((await removed.Content.ReadFromJsonAsync<MemberResponse>())!.FixedDesk);
    }

    [Fact]
    public async Task Update_HotDeskAsFixedDesk_Returns422Invalid()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var member = await CreateAsync(reception.Client, NewMember($"{UniqueTag()}@example.com"));
        var hotDesk = await SeedDeskAsync(WorkspaceType.HotDesk);

        var response = await reception.Client.PutAsJsonAsync(
            $"/api/members/{member.Id}",
            new UpdateMemberRequest("Test", "Member", member.Email, member.Phone, hotDesk.Id)
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(MemberErrorCodes.FixedDeskInvalid, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Update_DeskOfAnotherMember_Returns409Taken()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var desk = await SeedDeskAsync(WorkspaceType.FixedDesk);
        await CreateAsync(reception.Client, NewMember($"{UniqueTag()}@example.com") with { FixedWorkspaceId = desk.Id });
        var second = await CreateAsync(reception.Client, NewMember($"{UniqueTag()}@example.com"));

        var response = await reception.Client.PutAsJsonAsync(
            $"/api/members/{second.Id}",
            new UpdateMemberRequest("Test", "Member", second.Email, second.Phone, desk.Id)
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(MemberErrorCodes.FixedDeskTaken, await ReadCodeAsync(response));
    }

    // ------------------------------------------------------------------ deactivate / delete

    [Fact]
    public async Task Deactivate_LoginIsRefused_ActivateAllowsItAgain()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var email = $"{UniqueTag()}@example.com";
        var member = await CreateAsync(reception.Client, NewMember(email));
        using var anonymous = factory.CreateHttpsClient();
        var login = new LoginRequest(email, AuthenticatedClientFactory.Password);

        Assert.Equal(HttpStatusCode.NoContent, (await reception.Client.PostAsync($"/api/members/{member.Id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", login)).StatusCode);

        await reception.Client.PostAsync($"/api/members/{member.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/auth/login", login)).StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutHistory_Returns204_ThenGetReturns404()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var member = await CreateAsync(reception.Client, NewMember($"{UniqueTag()}@example.com"));

        var response = await reception.Client.DeleteAsync($"/api/members/{member.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reception.Client.GetAsync($"/api/members/{member.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_WithAMembership_Returns409InUse()
    {
        using var reception = await factory.CreateClientAsAsync(Roles.Receptionist);
        var member = await CreateAsync(reception.Client, NewMember($"{UniqueTag()}@example.com"));
        var type = await factory.SeedMembershipTypeAsync($"Daily {UniqueTag()}", 1, 15m);
        var sold = await reception.Client.PostAsJsonAsync(
            $"/api/members/{member.Id}/memberships",
            new { membershipTypeId = type.Id }
        );
        Assert.Equal(HttpStatusCode.Created, sold.StatusCode);

        var response = await reception.Client.DeleteAsync($"/api/members/{member.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("conflict.in_use", await ReadCodeAsync(response));
    }

    // ------------------------------------------------------------------ helpers

    private static CreateMemberRequest NewMember(string email) =>
        new("Test", "Member", email, "+387 61 555 666", AuthenticatedClientFactory.Password, null);

    private static async Task<MemberResponse> CreateAsync(HttpClient client, CreateMemberRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/members", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MemberResponse>())!;
    }

    private async Task<Workspace> SeedDeskAsync(WorkspaceType type)
    {
        var room = await factory.SeedRoomAsync($"Room {UniqueTag()}");
        return await factory.SeedWorkspaceAsync(room.Id, type);
    }
}
