using System.Net;
using System.Net.Http.Json;
using Coworking.Application.Auth.Dtos;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace Coworking.IntegrationTests.Authorization;

/// <summary>
/// NFR01/NFR05: passwords and tokens must never end up in the logs (log files are read by more
/// people than the database, and are often kept longer). Runs a whole session – register, failed
/// login, login, refresh, reuse attack, logout – and then searches EVERY log event.
/// </summary>
public class SensitiveDataLoggingTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task NFR01_PasswordsAndTokens_NeverAppearInLogs()
    {
        var sink = new CapturingLogSink();
        await using var loggedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILogEventSink>(sink))
        );
        using var client = loggedFactory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false }
        );

        const string password = "Very-Secret-Passw0rd-4711";
        var email = $"logs-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterMemberRequest("Log", "Tester", email, "+387 61 000 000", password)
        );
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var firstRefresh = RefreshTokenFrom(register);
        var firstAccess = (await register.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;

        var failedLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Totally-Wrong-99"));
        Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);

        var refreshed = await SendWithCookieAsync(client, "/api/auth/refresh", firstRefresh);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var secondRefresh = RefreshTokenFrom(refreshed);

        // Reuse of the first cookie → reuse detection logs a Warning: it must not include the token.
        var reuse = await SendWithCookieAsync(client, "/api/auth/refresh", firstRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        await SendWithCookieAsync(client, "/api/auth/logout", secondRefresh);

        var logs = sink.AllText();
        Assert.NotEmpty(sink.Events); // the sink really received the logs
        Assert.Contains("reuse", logs, StringComparison.OrdinalIgnoreCase); // and the interesting ones
        Assert.DoesNotContain(password, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Totally-Wrong-99", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(firstRefresh, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(secondRefresh, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(firstAccess, logs, StringComparison.Ordinal);
    }

    private static string RefreshTokenFrom(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie")).Split(';')[0]["cw_refresh=".Length..];

    private static async Task<HttpResponseMessage> SendWithCookieAsync(HttpClient client, string url, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Cookie", $"cw_refresh={refreshToken}");
        return await client.SendAsync(request);
    }
}
