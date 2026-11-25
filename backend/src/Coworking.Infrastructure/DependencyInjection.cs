using Coworking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Coworking.Infrastructure;

/// <summary>Registers the Infrastructure layer (database access) in the DI container.</summary>
public static class DependencyInjection
{
    /// <summary>Name of the connection string: configuration key "ConnectionStrings:Coworking".</summary>
    public const string ConnectionStringName = "Coworking";

    /// <summary>
    /// Adds <see cref="CoworkingDbContext"/> with PostgreSQL (Npgsql) and snake_case naming.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// The connection string is read LAZILY, the first time a DbContext is created, not at startup.
    /// So parts of the app that don't use the database (and their tests) work without one,
    /// and integration tests can supply a Testcontainers connection string at any point.
    /// </remarks>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<CoworkingDbContext>(
            (serviceProvider, options) =>
            {
                var connectionString =
                    serviceProvider
                        .GetRequiredService<IConfiguration>()
                        .GetConnectionString(ConnectionStringName)
                    ?? throw new InvalidOperationException(
                        $"Connection string 'ConnectionStrings:{ConnectionStringName}' is not configured. "
                            + "In development set it with dotnet user-secrets (see CW-7)."
                    );

                options
                    .UseNpgsql(connectionString)
                    // StartsAt -> starts_at, PK_Members -> pk_members, etc.
                    .UseSnakeCaseNamingConvention();
            }
        );

        return services;
    }
}
