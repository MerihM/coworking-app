using Coworking.Application.Auth;
using Coworking.Application.Common.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Coworking.IntegrationTests.Fixtures;

/// <summary>
/// Runs the real API in memory with the test JWT settings and <see cref="AuthTestController"/>.
/// No database is needed: the connection string is only read when a DbContext is created.
/// </summary>
public sealed class AuthTestingApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(TestAuthSettings.Values));

        builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(AuthTestController).Assembly));
    }

    /// <summary>Issues a token with the app's own token service (same key and settings).</summary>
    public string CreateToken(AuthUser user) =>
        Services.GetRequiredService<IAccessTokenService>().Create(user).Token;
}
