using Coworking.Application.Auth.Validators;
using Coworking.Application.Common;
using Coworking.Application.Common.Abstractions;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coworking.Infrastructure.Seeding;

/// <summary>
/// Creates the first administrator account (CW-12). Without it, nobody could log in to create
/// staff accounts: FR12 says only an administrator creates accounts, and there is no
/// "register as administrator" on purpose.
/// </summary>
/// <remarks>
/// <para>
/// Called once from Program.cs right after the app is built. It does something only when
/// <c>Seed:Admin:Email</c> and <c>Seed:Admin:Password</c> are configured AND there is no active
/// administrator yet, so running it on every start is safe (idempotent).
/// </para>
/// <para>
/// When the section is missing it returns immediately WITHOUT touching the database. Integration
/// tests and <c>dotnet ef</c> rely on that: they start the app before the database is migrated.
/// </para>
/// </remarks>
public static class AdminSeeder
{
    private const string LoggerName = "Coworking.Infrastructure.Seeding.AdminSeeder";

    /// <summary>Reads <c>Seed:Admin</c> from configuration and seeds when it is set.</summary>
    /// <param name="services">The app's root service provider (<c>app.Services</c>).</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns><c>true</c> when an administrator was created.</returns>
    public static Task<bool> SeedAdministratorAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services
            .GetRequiredService<IConfiguration>()
            .GetSection(SeedAdminOptions.SectionName)
            .Get<SeedAdminOptions>();

        if (
            options is null
            || string.IsNullOrWhiteSpace(options.Email)
            || string.IsNullOrWhiteSpace(options.Password)
        )
        {
            services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(LoggerName)
                .LogInformation("Seed:Admin is not configured; skipping administrator seeding");
            return Task.FromResult(false);
        }

        return SeedAsync(services, options, cancellationToken);
    }

    /// <summary>
    /// Creates the administrator if no active administrator exists. Public so tests can pass the
    /// options directly instead of through configuration.
    /// </summary>
    /// <param name="services">A service provider with the DbContext and password hasher.</param>
    /// <param name="options">The account to create.</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns><c>true</c> when an administrator was created.</returns>
    public static async Task<bool> SeedAsync(
        IServiceProvider services,
        SeedAdminOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        // DbContext is scoped (one per request); outside a request we create a scope ourselves.
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerName);

        try
        {
            var hasActiveAdministrator = await db.Employees.AnyAsync(
                e => e.IsActive && e.Role == EmployeeRole.Administrator,
                cancellationToken
            );
            if (hasActiveAdministrator)
            {
                logger.LogDebug("An active administrator already exists; nothing to seed");
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.Email) || options.Password is not { Length: >= PasswordRules.MinLength })
            {
                logger.LogError(
                    "No active administrator exists, but Seed:Admin is incomplete (e-mail and a password of at least {MinLength} characters are required)",
                    PasswordRules.MinLength
                );
                return false;
            }

            var email = EmailAddresses.Normalize(options.Email);
            if (await db.IsEmailTakenAsync(email, cancellationToken: cancellationToken))
            {
                logger.LogError(
                    "Cannot seed administrator {Email}: the e-mail is already used by another account",
                    email
                );
                return false;
            }

            db.Employees.Add(
                new Employee(
                    options.FirstName,
                    options.LastName,
                    email,
                    passwordHasher.Hash(options.Password),
                    EmployeeRole.Administrator
                )
            );
            await db.SaveChangesAsync(cancellationToken);

            // The password is never logged (NFR01).
            logger.LogInformation("Created the first administrator account {Email}", email);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // E.g. the database is not running or not migrated yet. Don't crash the whole API for
            // this; /health shows the database problem, and the next start tries again.
            logger.LogError(exception, "Administrator seeding failed");
            return false;
        }
    }
}
