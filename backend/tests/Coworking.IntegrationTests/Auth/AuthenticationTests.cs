using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Coworking.Application.Auth;
using Coworking.Infrastructure.Auth;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Coworking.IntegrationTests.Auth;

/// <summary>
/// End-to-end checks of the JwtBearer setup through real HTTP requests (NFR01).
/// </summary>
public sealed class AuthenticationTests(AuthTestingApiFactory factory) : IClassFixture<AuthTestingApiFactory>
{
    private static readonly AuthUser Member =
        new(42, UserType.Member, Roles.Member, "member@example.com", "Test Member");

    private static readonly AuthUser Manager =
        new(7, UserType.Employee, Roles.Manager, "manager@example.com", "Test Manager");

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401ProblemDetails()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/test/auth/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
        Assert.Equal(AuthErrorCodes.Unauthenticated, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200AndCurrentUser()
    {
        using var client = ClientWithToken(factory.CreateToken(Member));

        var me = await client.GetFromJsonAsync<WhoAmIResponse>("/test/auth/whoami");

        Assert.NotNull(me);
        Assert.True(me.IsAuthenticated);
        Assert.Equal(42, me.Id);
        Assert.Equal(nameof(UserType.Member), me.UserType);
        Assert.Equal(Roles.Member, me.Role);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        // Issued one hour ago with a 15-minute lifetime → expired 45 minutes ago (far beyond the 30 s skew).
        var oneHourAgo = new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(-1));
        var token = new AccessTokenService(Options.Create(TestAuthSettings.CreateJwtOptions()), oneHourAgo).Create(Member);
        using var client = ClientWithToken(token.Token);

        var response = await client.GetAsync("/test/auth/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_Returns401()
    {
        // Simulates a forged token: correct claims, but the attacker doesn't know our key.
        var forgedOptions = TestAuthSettings.CreateJwtOptions(signingKey: new string('x', 48));
        var token = new AccessTokenService(Options.Create(forgedOptions), TimeProvider.System).Create(Manager);
        using var client = ClientWithToken(token.Token);

        var response = await client.GetAsync("/test/auth/manager");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GarbageToken_Returns401()
    {
        using var client = ClientWithToken("this.is.not-a-jwt");

        var response = await client.GetAsync("/test/auth/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongRole_Returns403ProblemDetails()
    {
        using var client = ClientWithToken(factory.CreateToken(Member));

        var response = await client.GetAsync("/test/auth/manager");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(AuthErrorCodes.Forbidden, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task RightRole_Returns200()
    {
        using var client = ClientWithToken(factory.CreateToken(Manager));

        var response = await client.GetAsync("/test/auth/manager");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeTokenWithMemberRole_IsNotAMember()
    {
        // Policies check user_type AND role: an employee token must never pass as a member
        // (and the reverse), even if the role claim were wrong.
        var strange = new AuthUser(9, UserType.Member, Roles.Manager, "x@example.com", "Odd Token");
        using var client = ClientWithToken(factory.CreateToken(strange));

        var response = await client.GetAsync("/test/auth/manager");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AllowAnonymousEndpoint_WorksWithoutToken()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/test/auth/anonymous");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_IsNotProtected()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        // No database in this factory, so /health may answer 503 – the point is that it's not 401.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_ContainsBearerScheme()
    {
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(json);

        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
