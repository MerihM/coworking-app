using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coworking.Application.Auth;
using Coworking.Application.Auth.Dtos;
using Coworking.Application.Common.Abstractions;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Employees;
using Coworking.Application.Employees.Dtos;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coworking.IntegrationTests.Employees;

/// <summary>
/// /api/employees end-to-end (FR12, UC14): CRUD, paging and search, the self-change and
/// last-administrator rules, session revocation, FK-protected delete, and access control.
/// </summary>
/// <remarks>
/// Callers get a token straight from the app's IAccessTokenService (faster than logging in);
/// the login endpoint itself is used where the test is ABOUT logging in. Every test seeds its
/// own employees with unique e-mails/names, because the database is shared by the whole class.
/// </remarks>
public class EmployeesEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string Password = "Coworking123";

    /// <summary>The API sends enums as names ("Manager"), so the test client must read them that way.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // ------------------------------------------------------------------ access control

    [Fact]
    public async Task List_Anonymous_Returns401()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(EmployeeRole.Receptionist)]
    [InlineData(EmployeeRole.Manager)]
    public async Task List_OtherStaffRoles_Return403(EmployeeRole role)
    {
        var (client, _) = await ClientForNewEmployeeAsync(role);

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------ list, get

    [Fact]
    public async Task List_SearchAndPaging_ReturnsTheRightPage()
    {
        var (admin, _) = await AdminClientAsync();
        var tag = UniqueTag();
        foreach (var firstName in new[] { "Ana", "Bojan", "Cvijeta" })
        {
            await SeedEmployeeAsync(EmployeeRole.Receptionist, firstName: firstName, lastName: tag);
        }

        var page1 = await admin.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            $"/api/employees?search={tag.ToUpperInvariant()}&pageSize=2&page=1",
            Json
        );
        var page2 = await admin.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            $"/api/employees?search={tag}&pageSize=2&page=2",
            Json
        );

        Assert.Equal(3, page1!.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(new[] { "Ana", "Bojan" }, page1.Items.Select(e => e.FirstName));
        Assert.Equal(new[] { "Cvijeta" }, page2!.Items.Select(e => e.FirstName));
    }

    [Fact]
    public async Task List_SearchByFullName_Works()
    {
        var (admin, _) = await AdminClientAsync();
        var tag = UniqueTag();
        await SeedEmployeeAsync(EmployeeRole.Manager, firstName: "Selma", lastName: tag);

        var result = await admin.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            $"/api/employees?search=selma%20{tag}",
            Json
        );

        Assert.Equal(1, result!.TotalCount);
    }

    [Fact]
    public async Task List_FiltersByRoleAndStatus()
    {
        var (admin, _) = await AdminClientAsync();
        var tag = UniqueTag();
        await SeedEmployeeAsync(EmployeeRole.Manager, lastName: tag);
        await SeedEmployeeAsync(EmployeeRole.Manager, lastName: tag, isActive: false);
        await SeedEmployeeAsync(EmployeeRole.Receptionist, lastName: tag);

        var result = await admin.GetFromJsonAsync<PagedResult<EmployeeResponse>>(
            $"/api/employees?search={tag}&role=Manager&isActive=true",
            Json
        );

        var only = Assert.Single(result!.Items);
        Assert.Equal(EmployeeRole.Manager, only.Role);
        Assert.True(only.IsActive);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=500")]
    public async Task List_InvalidPaging_Returns400(string queryString)
    {
        var (admin, _) = await AdminClientAsync();

        var response = await admin.GetAsync($"/api/employees?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var (admin, _) = await AdminClientAsync();

        var response = await admin.GetAsync("/api/employees/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ create, update

    [Fact]
    public async Task Create_Returns201_AndTheNewEmployeeCanLogIn()
    {
        var (admin, _) = await AdminClientAsync();
        var email = UniqueEmail();

        var response = await admin.PostAsJsonAsync(
            "/api/employees",
            new CreateEmployeeRequest("Emir", "Kovač", email, EmployeeRole.Receptionist, Password),
            Json
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EmployeeResponse>(Json);
        Assert.EndsWith($"/api/employees/{created!.Id}", response.Headers.Location?.ToString(), StringComparison.Ordinal);
        Assert.Equal(EmployeeRole.Receptionist, created.Role);
        Assert.True(created.IsActive);

        var login = await LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(Roles.Receptionist, session!.User.Role);
    }

    [Fact]
    public async Task Create_RoleIsSentAsString()
    {
        var (admin, _) = await AdminClientAsync();

        // Raw JSON, exactly like the frontend will send it.
        var response = await admin.PostAsync(
            "/api/employees",
            JsonContent.Create(
                new { firstName = "Lejla", lastName = "Begić", email = UniqueEmail(), role = "Manager", password = Password }
            )
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Manager", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Create_EmailOfAMember_Returns409()
    {
        var (admin, _) = await AdminClientAsync();
        var memberEmail = await SeedMemberAsync();

        var response = await admin.PostAsJsonAsync(
            "/api/employees",
            new CreateEmployeeRequest("Emir", "Kovač", memberEmail.ToUpperInvariant(), EmployeeRole.Manager, Password),
            Json
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AuthErrorCodes.EmailTaken, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Create_InvalidInput_Returns400()
    {
        var (admin, _) = await AdminClientAsync();

        var response = await admin.PostAsJsonAsync(
            "/api/employees",
            new CreateEmployeeRequest("", "Kovač", "nope", EmployeeRole.Manager, "weak"),
            Json
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("firstName", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Update_ChangesNameEmailAndRole()
    {
        var (admin, _) = await AdminClientAsync();
        var employee = await SeedEmployeeAsync(EmployeeRole.Receptionist);
        var newEmail = UniqueEmail();

        var response = await admin.PutAsJsonAsync(
            $"/api/employees/{employee.Id}",
            new UpdateEmployeeRequest("Novo", "Ime", $" {newEmail.ToUpperInvariant()} ", EmployeeRole.Manager),
            Json
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EmployeeResponse>(Json);
        Assert.Equal("Novo", updated!.FirstName);
        Assert.Equal(newEmail, updated.Email);
        Assert.Equal(EmployeeRole.Manager, updated.Role);
    }

    [Fact]
    public async Task Update_OwnName_IsAllowed_ButOwnRole_Returns422()
    {
        var (admin, me) = await AdminClientAsync();

        var rename = await admin.PutAsJsonAsync(
            $"/api/employees/{me.Id}",
            new UpdateEmployeeRequest("Renamed", me.LastName, me.Email, EmployeeRole.Administrator),
            Json
        );
        var demote = await admin.PutAsJsonAsync(
            $"/api/employees/{me.Id}",
            new UpdateEmployeeRequest(me.FirstName, me.LastName, me.Email, EmployeeRole.Manager),
            Json
        );

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, demote.StatusCode);
        Assert.Equal(EmployeeErrorCodes.SelfChange, await ReadCodeAsync(demote));
    }

    // ------------------------------------------------------------------ deactivate, activate, reset

    [Fact]
    public async Task Deactivate_EndsSessions_AndBlocksLogin_ActivateRestoresIt()
    {
        var (admin, _) = await AdminClientAsync();
        var employee = await SeedEmployeeAsync(EmployeeRole.Receptionist);
        var refreshCookie = GetRefreshCookie(await LoginAsync(employee.Email, Password));

        var deactivate = await admin.PostAsync($"/api/employees/{employee.Id}/deactivate", content: null);

        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(employee.Email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(refreshCookie)).StatusCode);

        var activate = await admin.PostAsync($"/api/employees/{employee.Id}/activate", content: null);

        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(employee.Email, Password)).StatusCode);
    }

    [Fact]
    public async Task Deactivate_Self_Returns422()
    {
        var (admin, me) = await AdminClientAsync();

        var response = await admin.PostAsync($"/api/employees/{me.Id}/deactivate", content: null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(EmployeeErrorCodes.SelfChange, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Deactivate_LastActiveAdministrator_Returns422()
    {
        // The only way to reach this rule: administrator A was deactivated a moment ago, but A's
        // access token is still valid (up to 15 min). A now tries to deactivate B, the last
        // active administrator. Without the rule nobody could manage accounts any more.
        var (callerA, _) = await AdminClientAsync();
        var adminB = await SeedEmployeeAsync(EmployeeRole.Administrator);

        // Deactivates A too (its token was issued before, so it still works) and every
        // administrator left over from other tests: B is now the only active administrator.
        await DeactivateAllAdministratorsExceptAsync(adminB.Id);

        var response = await callerA.PostAsync($"/api/employees/{adminB.Id}/deactivate", content: null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(EmployeeErrorCodes.LastAdministrator, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task ResetPassword_OldPasswordStopsWorking_AndSessionsEnd()
    {
        var (admin, _) = await AdminClientAsync();
        var employee = await SeedEmployeeAsync(EmployeeRole.Manager);
        var refreshCookie = GetRefreshCookie(await LoginAsync(employee.Email, Password));

        var response = await admin.PostAsJsonAsync(
            $"/api/employees/{employee.Id}/reset-password",
            new ResetPasswordRequest("BrandNew2026"),
            Json
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(employee.Email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(employee.Email, "BrandNew2026")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(refreshCookie)).StatusCode);
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task Delete_UnreferencedEmployee_Returns204_ThenGetReturns404()
    {
        var (admin, _) = await AdminClientAsync();
        var employee = await SeedEmployeeAsync(EmployeeRole.Receptionist);

        var delete = await admin.DeleteAsync($"/api/employees/{employee.Id}");
        var get = await admin.GetAsync($"/api/employees/{employee.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Delete_EmployeeWhoRecordedAPayment_Returns409InUse()
    {
        var (admin, _) = await AdminClientAsync();
        var receptionist = await SeedEmployeeAsync(EmployeeRole.Receptionist);
        await RecordPaymentByAsync(receptionist.Id);

        var response = await admin.DeleteAsync($"/api/employees/{receptionist.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("conflict.in_use", await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Delete_Self_Returns422()
    {
        var (admin, me) = await AdminClientAsync();

        var response = await admin.DeleteAsync($"/api/employees/{me.Id}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ------------------------------------------------------------------ helpers

    private HttpClient CreateClient() =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false }
        );

    private static string UniqueTag() => "t" + Guid.NewGuid().ToString("N")[..10];

    private static string UniqueEmail(string prefix = "staff") => $"{prefix}-{Guid.NewGuid():N}@example.com";

    /// <summary>A new active administrator and a client that calls the API as them.</summary>
    private Task<(HttpClient Client, Employee Admin)> AdminClientAsync() =>
        ClientForNewEmployeeAsync(EmployeeRole.Administrator);

    private async Task<(HttpClient Client, Employee Employee)> ClientForNewEmployeeAsync(EmployeeRole role)
    {
        var employee = await SeedEmployeeAsync(role);
        var token = factory.Services.GetRequiredService<IAccessTokenService>().Create(AuthUser.FromEmployee(employee));

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return (client, employee);
    }

    private async Task<Employee> SeedEmployeeAsync(
        EmployeeRole role,
        string firstName = "Test",
        string lastName = "Employee",
        bool isActive = true
    )
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var employee = new Employee(firstName, lastName, UniqueEmail(), hasher.Hash(Password), role);
        if (!isActive)
        {
            employee.Deactivate();
        }

        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee;
    }

    private async Task<string> SeedMemberAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();

        var member = new Member("Test", "Member", UniqueEmail("member"), "+387 61 000 000", "hash", DateTimeOffset.UtcNow);
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member.Email;
    }

    /// <summary>The employee records a membership payment, so payments.employee_id references them.</summary>
    private async Task RecordPaymentByAsync(int employeeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var now = DateTimeOffset.UtcNow;

        var employee = await db.Employees.SingleAsync(e => e.Id == employeeId);
        var member = new Member("Paying", "Member", UniqueEmail("member"), "+387 61 000 000", "hash", now);
        var membership = member.PurchaseMembership(new MembershipType($"Monthly {UniqueTag()}", 30, 120m), now);
        employee.RecordPayment(membership, 120m, PaymentMethod.Cash, now);

        db.Members.Add(member);
        await db.SaveChangesAsync();
    }

    /// <summary>Simulates "everyone else was deactivated" for the last-administrator test.</summary>
    private async Task DeactivateAllAdministratorsExceptAsync(int keepActiveId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();

        await db
            .Employees.Where(e => e.Role == EmployeeRole.Administrator && e.Id != keepActiveId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.IsActive, false));
    }

    private async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
    }

    private async Task<HttpResponseMessage> RefreshAsync(string refreshToken)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"cw_refresh={refreshToken}");
        return await client.SendAsync(request);
    }

    private static string GetRefreshCookie(HttpResponseMessage loginResponse)
    {
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var setCookie = Assert.Single(loginResponse.Headers.GetValues("Set-Cookie"));
        return setCookie.Split(';')[0]["cw_refresh=".Length..];
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}
