using Coworking.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Coworking.IntegrationTests.ErrorHandling;

/// <summary>
/// The real API (same Program.cs) plus ErrorTestController.
/// ConfigureTestServices runs AFTER Program.cs registered its services, so tests can add
/// or replace things. CW-7 uses the same pattern to point the API at a test database.
/// </summary>
public sealed class ErrorTestingApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // CW-9: the API validates the Jwt settings at startup and refuses to start without a
        // signing key. Locally user-secrets would provide one, but CI has no user-secrets,
        // so every factory supplies the shared test settings itself.
        builder.ConfigureAppConfiguration(
            (_, configuration) => configuration.AddInMemoryCollection(TestAuthSettings.Values)
        );

        builder.ConfigureTestServices(services =>
            // MVC only discovers controllers in the API assembly by default;
            // this adds the test assembly as an extra "application part".
            services.AddControllers().AddApplicationPart(typeof(ErrorTestController).Assembly)
        );
    }
}
