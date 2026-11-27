using System.Net;
using System.Net.Http.Headers;
using Coworking.Application.Auth;
using Coworking.IntegrationTests.Fixtures;

namespace Coworking.IntegrationTests.Authorization;

/// <summary>
/// NFR01 "every user has access only to the functions of their role", checked for EVERY policy
/// and EVERY role: 6 endpoints × 5 callers = 30 cases. Tokens are issued directly by the app's
/// token service (no database needed); the real login flow is covered by RoleAccessTests.
/// </summary>
public sealed class PolicyMatrixTests(AuthTestingApiFactory factory) : IClassFixture<AuthTestingApiFactory>
{
    private const string Anonymous = "Anonymous";

    /// <summary>Which callers may use each test endpoint. Everyone else gets 403 (or 401 when anonymous).</summary>
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["authenticated"] = [Roles.Member, Roles.Receptionist, Roles.Manager, Roles.Administrator],
        ["administrator"] = [Roles.Administrator],
        ["manager"] = [Roles.Manager],
        ["receptionist-or-manager"] = [Roles.Receptionist, Roles.Manager],
        ["staff"] = [Roles.Receptionist, Roles.Manager, Roles.Administrator],
        ["member"] = [Roles.Member],
    };

    private static readonly string[] Callers = [Anonymous, .. Roles.All];

    public static TheoryData<string, string, HttpStatusCode> Matrix()
    {
        var data = new TheoryData<string, string, HttpStatusCode>();
        foreach (var (endpoint, allowedRoles) in Allowed)
        {
            foreach (var caller in Callers)
            {
                var expected =
                    caller == Anonymous ? HttpStatusCode.Unauthorized
                    : allowedRoles.Contains(caller) ? HttpStatusCode.OK
                    : HttpStatusCode.Forbidden;
                data.Add(endpoint, caller, expected);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task NFR01_EachRoleReachesOnlyItsOwnFunctions(string endpoint, string caller, HttpStatusCode expected)
    {
        using var client = ClientFor(caller);

        var response = await client.GetAsync($"/test/policies/{endpoint}");

        Assert.Equal(expected, response.StatusCode);
    }

    // TC08 (Member opens the FR11 report → 403) moved to Reports/ReportsEndpointsTests in CW-32,
    // where it runs against the real report endpoint instead of a test stand-in.

    private HttpClient ClientFor(string caller)
    {
        var client = factory.CreateClient();
        if (caller != Anonymous)
        {
            var user = caller == Roles.Member
                ? new AuthUser(1, UserType.Member, Roles.Member, "member@example.com", "Test Member")
                : new AuthUser(2, UserType.Employee, caller, "staff@example.com", $"Test {caller}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(user));
        }

        return client;
    }
}
