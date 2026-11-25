using Coworking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Coworking.IntegrationTests.Fixtures;

/// <summary>
/// The real API connected to a throwaway PostgreSQL 18 container.
/// xUnit calls InitializeAsync before the first test of a class that uses this fixture:
/// it starts the container and applies all EF Core migrations (including the EXCLUDE constraint).
/// Requires Docker to be running.
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same image as docker-compose.yml, so tests run against the production database version.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(
        "postgres:18-alpine"
    ).Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        // Accessing Services builds the API host (ConfigureWebHost runs now, after the container started).
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync() => await _postgres.DisposeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Added LAST, so these values override the developer's user-secrets:
        // tests never touch the local coworking_db and never depend on a local signing key.
        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Coworking"] = _postgres.GetConnectionString(),
                    }
                );

                // CW-9: Jwt settings are validated at startup (CI has no user-secrets).
                configuration.AddInMemoryCollection(TestAuthSettings.Values);
            }
        );
    }
}
