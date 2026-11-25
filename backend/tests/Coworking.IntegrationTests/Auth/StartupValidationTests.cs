using Coworking.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Coworking.IntegrationTests.Auth;

/// <summary>
/// The app must refuse to start with a missing or weak signing key, instead of starting and then
/// failing (or worse, signing with a guessable key) at the first login.
/// </summary>
public sealed class StartupValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("too-short-key")]
    public void InvalidSigningKey_AppFailsToStartWithClearMessage(string signingKey)
    {
        using var baseFactory = new AuthTestingApiFactory();

        // Configuration added here comes after the factory's own, so it overrides the valid test key.
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = signingKey })));

        // CreateClient() starts the app; ValidateOnStart throws during startup.
        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("SigningKey", exception.ToString(), StringComparison.Ordinal);
    }
}
