using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
        builder.ConfigureTestServices(services =>
            // MVC only discovers controllers in the API assembly by default;
            // this adds the test assembly as an extra "application part".
            services.AddControllers().AddApplicationPart(typeof(ErrorTestController).Assembly)
        );
    }
}
