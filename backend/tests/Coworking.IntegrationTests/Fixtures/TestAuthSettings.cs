using Coworking.Infrastructure.Auth;

namespace Coworking.IntegrationTests.Fixtures;

/// <summary>
/// Auth-related settings for tests. Every WebApplicationFactory must add <see cref="Values"/> to its
/// configuration: the app validates Jwt options at startup, and CI has no user-secrets.
/// The key below is public on GitHub on purpose – it is only ever used by tests.
/// </summary>
public static class TestAuthSettings
{
    public const string Issuer = "coworking-api-tests";
    public const string Audience = "coworking-web-tests";
    public const string SigningKey = "integration-tests-only-signing-key-0123456789abcdef";

    /// <summary>Configuration entries to pass to <c>AddInMemoryCollection</c>.</summary>
    public static Dictionary<string, string?> Values => new()
    {
        ["Jwt:Issuer"] = Issuer,
        ["Jwt:Audience"] = Audience,
        ["Jwt:SigningKey"] = SigningKey,

        // CW-11: in tests every request comes from the same (empty) IP address, so the real limit
        // of 5 per minute would make unrelated tests fail with 429. The rate-limit test sets 5 again.
        ["RateLimiting:AuthPermitLimit"] = "100000",
    };

    /// <summary>The same settings as an options object, for creating tokens outside the app.</summary>
    /// <param name="signingKey">Override the key, e.g. to create a token the app must reject.</param>
    public static JwtOptions CreateJwtOptions(string signingKey = SigningKey) =>
        new()
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = signingKey,
            AccessTokenMinutes = 15,
        };
}
