using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Coworking.Application.Auth;
using Coworking.Application.Auth.Dtos;
using Coworking.Application.Common.Abstractions;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Coworking.IntegrationTests.Auth;

/// <summary>
/// The /api/auth endpoints end-to-end: HTTP → controller → AuthService → PostgreSQL (FR01, NFR01).
/// </summary>
/// <remarks>
/// Clients use "https://localhost" (the refresh cookie is Secure) and handle cookies MANUALLY
/// (HandleCookies = false): each test reads Set-Cookie itself and sends "Cookie: cw_refresh=..."
/// itself, so it can check the cookie's attributes and replay an old cookie after logout.
/// </remarks>
public class AuthEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private const string Password = "Coworking123";

    // ------------------------------------------------------------------ register

    [Fact]
    public async Task Register_ValidInput_Returns201WithAccessTokenAndRefreshCookie()
    {
        using var client = CreateClient();
        var request = NewRegistration(email: $"  New.Member-{Guid.NewGuid():N}@Example.com ");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith("/api/auth/me", response.Headers.Location?.ToString(), StringComparison.Ordinal);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.AccessToken));
        Assert.Equal(UserTypeClaimValues.Member, body.User.UserType);
        Assert.Equal(Roles.Member, body.User.Role);
        Assert.Equal(request.Email.Trim().ToLowerInvariant(), body.User.Email);
        Assert.Equal("Amra Hodžić", body.User.FullName);

        var cookie = GetSetCookie(response).ToLowerInvariant();
        Assert.StartsWith("cw_refresh=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=strict", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/api/auth", cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_StoresOnlyAPasswordHash()
    {
        using var client = CreateClient();
        var request = NewRegistration();

        await client.PostAsJsonAsync("/api/auth/register", request);

        var hash = await QueryAsync(db =>
            db.Members.Where(m => m.Email == request.Email).Select(m => m.PasswordHash).SingleAsync()
        );
        Assert.NotEqual(Password, hash);
        Assert.StartsWith("AQAAAAI", hash, StringComparison.Ordinal); // Identity v3, PBKDF2-SHA512
    }

    [Fact]
    public async Task Register_DuplicateEmailInDifferentCase_Returns409EmailTaken()
    {
        using var client = CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/api/auth/register", NewRegistration(email));

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            NewRegistration(email.ToUpperInvariant())
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AuthErrorCodes.EmailTaken, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Register_EmailOfAnEmployee_Returns409EmailTaken()
    {
        // One login form for both tables, so the e-mail must be unique across both.
        var employeeEmail = await SeedEmployeeAsync(EmployeeRole.Receptionist);
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRegistration(employeeEmail));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AuthErrorCodes.EmailTaken, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Register_InvalidInput_Returns400WithCamelCaseFieldErrors()
    {
        using var client = CreateClient();
        var request = new RegisterMemberRequest("", "Hodžić", "not-an-email", "phone?", "short");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("firstName", out _));
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("phone", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.False(errors.TryGetProperty("lastName", out _));
    }

    [Fact]
    public async Task Register_MissingFields_Returns400FromOurValidator()
    {
        // MVC's implicit [Required] is switched off, so even an empty JSON object reaches
        // FluentValidation and gets our (translatable) messages for every field.
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal(5, errors.EnumerateObject().Count());
    }

    [Theory]
    [InlineData(null, "Password must contain at least one digit.")]
    [InlineData("bs", "Lozinka mora sadržavati najmanje jednu cifru.")]
    [InlineData("bs-BA", "Lozinka mora sadržavati najmanje jednu cifru.")]
    [InlineData("de", "Password must contain at least one digit.")] // unsupported → English
    public async Task Register_ValidationMessages_FollowAcceptLanguage(
        string? language,
        string expectedMessage
    )
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(NewRegistration() with { Password = "abcdefgh" }),
        };
        if (language is not null)
        {
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal(expectedMessage, errors.GetProperty("password")[0].GetString());
    }

    // ------------------------------------------------------------------ login

    [Fact]
    public async Task Login_Member_IgnoresEmailCaseAndSpaces()
    {
        using var client = CreateClient();
        var registration = NewRegistration();
        await client.PostAsJsonAsync("/api/auth/register", registration);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest($"  {registration.Email.ToUpperInvariant()} ", Password)
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(Roles.Member, body!.User.Role);
        Assert.StartsWith("cw_refresh=", GetSetCookie(response), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EmployeeRole.Receptionist)]
    [InlineData(EmployeeRole.Manager)]
    [InlineData(EmployeeRole.Administrator)]
    public async Task Login_Employee_ReturnsEmployeeAndRole(EmployeeRole role)
    {
        var email = await SeedEmployeeAsync(role);
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(UserTypeClaimValues.Employee, body!.User.UserType);
        Assert.Equal(role.ToString(), body.User.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401InvalidCredentials()
    {
        using var client = CreateClient();
        var registration = NewRegistration();
        await client.PostAsJsonAsync("/api/auth/register", registration);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(registration.Email, "Wrong-password-1")
        );

        await AssertInvalidCredentialsAsync(response);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401InvalidCredentials()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(UniqueEmail("nobody"), Password)
        );

        // Same status and code as a wrong password: the API doesn't reveal which e-mails exist.
        await AssertInvalidCredentialsAsync(response);
    }

    [Fact]
    public async Task Login_DeactivatedEmployee_Returns401InvalidCredentials()
    {
        var email = await SeedEmployeeAsync(EmployeeRole.Receptionist, isActive: false);
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));

        await AssertInvalidCredentialsAsync(response);
    }

    [Fact]
    public async Task Login_EmptyFields_Returns400()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_SixthAttemptWithinAMinute_Returns429WithRetryAfter()
    {
        // A separate API instance with the real limit of 5 (the shared one has a huge test limit).
        await using var limitedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = "5" }
                    )
            )
        );
        using var client = limitedFactory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false }
        );
        var attempt = new LoginRequest(UniqueEmail("brute"), "Wrong-password-1");

        for (var i = 0; i < 5; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", attempt);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var rejected = await client.PostAsJsonAsync("/api/auth/login", attempt);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal(AuthErrorCodes.TooManyRequests, await ReadCodeAsync(rejected));
    }

    // ------------------------------------------------------------------ refresh / logout / me

    [Fact]
    public async Task Refresh_WithValidCookie_ReturnsNewAccessTokenAndRotatedCookie()
    {
        using var client = CreateClient();
        var registered = await client.PostAsJsonAsync("/api/auth/register", NewRegistration());
        var firstCookie = GetRefreshTokenValue(registered);

        var response = await SendWithCookieAsync(client, HttpMethod.Post, "/api/auth/refresh", firstCookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrEmpty(body!.AccessToken));
        Assert.NotEqual(firstCookie, GetRefreshTokenValue(response));
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401AndDeletesCookie()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(AuthErrorCodes.RefreshInvalid, await ReadCodeAsync(response));

        // Deleting a cookie = sending it again with an expiry date in the past (1970).
        Assert.Contains("1970", GetSetCookie(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_RevokesSessionOnTheServer()
    {
        using var client = CreateClient();
        var registered = await client.PostAsJsonAsync("/api/auth/register", NewRegistration());
        var cookie = GetRefreshTokenValue(registered);

        var logout = await SendWithCookieAsync(client, HttpMethod.Post, "/api/auth/logout", cookie);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("1970", GetSetCookie(logout), StringComparison.Ordinal);

        // Replaying the old cookie (as if it had been copied before logout) must not work.
        var refresh = await SendWithCookieAsync(client, HttpMethod.Post, "/api/auth/refresh", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutCookie_Returns204()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithAccessTokenFromRefresh_ReturnsCurrentUser()
    {
        using var client = CreateClient();
        var registration = NewRegistration();
        var registered = await client.PostAsJsonAsync("/api/auth/register", registration);
        var refreshed = await SendWithCookieAsync(
            client,
            HttpMethod.Post,
            "/api/auth/refresh",
            GetRefreshTokenValue(registered)
        );
        var accessToken = (await refreshed.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(registration.Email, me!.Email);
        Assert.Equal(Roles.Member, me.Role);
    }

    [Fact]
    public async Task Me_WithoutAccessToken_Returns401()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AuthErrorCodes.Unauthenticated, await ReadCodeAsync(response));
    }

    // ------------------------------------------------------------------ helpers

    private HttpClient CreateClient() =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = false,
            }
        );

    private static string UniqueEmail(string prefix = "member") =>
        $"{prefix}-{Guid.NewGuid():N}@example.com";

    private static RegisterMemberRequest NewRegistration(string? email = null) =>
        new("Amra", "Hodžić", email ?? UniqueEmail(), "+387 61 123 456", Password);

    private static async Task<HttpResponseMessage> SendWithCookieAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string refreshToken
    )
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", $"cw_refresh={refreshToken}");
        return await client.SendAsync(request);
    }

    /// <summary>The single Set-Cookie header of a response.</summary>
    private static string GetSetCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values), "No Set-Cookie header.");
        return Assert.Single(values);
    }

    /// <summary>The raw refresh token from "cw_refresh=VALUE; expires=...; path=...".</summary>
    private static string GetRefreshTokenValue(HttpResponseMessage response)
    {
        var firstPart = GetSetCookie(response).Split(';')[0];
        Assert.StartsWith("cw_refresh=", firstPart, StringComparison.Ordinal);
        return firstPart["cw_refresh=".Length..];
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("code").GetString();
    }

    private static async Task AssertInvalidCredentialsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AuthErrorCodes.InvalidCredentials, await ReadCodeAsync(response));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private async Task<T> QueryAsync<T>(Func<CoworkingDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<CoworkingDbContext>());
    }

    /// <summary>Saves an employee whose password is <see cref="Password"/>; returns the e-mail.</summary>
    private Task<string> SeedEmployeeAsync(EmployeeRole role, bool isActive = true) =>
        QueryAsync(async db =>
        {
            var hasher = factory.Services.GetRequiredService<IPasswordHasher>();
            var employee = new Employee("Test", "Employee", UniqueEmail("staff"), hasher.Hash(Password), role);
            if (!isActive)
            {
                employee.Deactivate();
            }

            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            return employee.Email;
        });
}
