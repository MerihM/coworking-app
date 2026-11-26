using System.Net;
using Coworking.Application.Auth;
using Coworking.IntegrationTests.Fixtures;

namespace Coworking.IntegrationTests.Authorization;

/// <summary>
/// The same NFR01 rule on REAL endpoints with REAL logins (users in PostgreSQL, token from
/// POST /api/auth/login) – proves the pieces work together, not just the policies in isolation.
/// </summary>
public class RoleAccessTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Theory]
    [InlineData(nameof(Roles.Member), HttpStatusCode.Forbidden)]
    [InlineData(nameof(Roles.Receptionist), HttpStatusCode.Forbidden)]
    [InlineData(nameof(Roles.Manager), HttpStatusCode.Forbidden)]
    [InlineData(nameof(Roles.Administrator), HttpStatusCode.OK)]
    public async Task NFR01_StaffManagement_OnlyAdministrator(string role, HttpStatusCode expected)
    {
        using var caller = await factory.CreateClientAsAsync(role);

        var response = await caller.Client.GetAsync("/api/employees");

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task NFR01_StaffManagement_Anonymous_Returns401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(nameof(Roles.Member), "member")]
    [InlineData(nameof(Roles.Receptionist), "employee")]
    [InlineData(nameof(Roles.Manager), "employee")]
    [InlineData(nameof(Roles.Administrator), "employee")]
    public async Task EveryRole_CanReadItsOwnAccount(string role, string expectedUserType)
    {
        using var caller = await factory.CreateClientAsAsync(role);

        var response = await caller.Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(role, caller.User.Role);
        Assert.Equal(expectedUserType, caller.User.UserType);
    }
}
