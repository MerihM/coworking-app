using Coworking.Application.Auth;
using Coworking.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Coworking.Infrastructure.Auth;

/// <summary>
/// Registers the authentication services that live in Infrastructure.
/// Called from <c>AddInfrastructure</c>.
/// </summary>
public static class AuthRegistration
{
    /// <summary>
    /// Adds JWT and refresh token options (validated at startup), the password hasher, the access
    /// and refresh token services, the expired-token cleanup job and the system clock.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services)
    {
        // TryAdd: tests can register a FakeTimeProvider first and it won't be overwritten.
        services.TryAddSingleton(TimeProvider.System);

        // BindConfiguration reads the section lazily (when the options are first used), so
        // configuration added by WebApplicationFactory in tests is visible.
        // ValidateDataAnnotations checks the [Required]/[MinLength]/[Range] attributes and
        // ValidateOnStart runs that check while the app starts, not on the first request.
        services
            .AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<RefreshTokenOptions>()
            .BindConfiguration(RefreshTokenOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Stateless and thread-safe: one instance for the whole app.
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<IAccessTokenService, AccessTokenService>();

        // Scoped: uses the DbContext of the current request.
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        // Daily delete of long-expired refresh tokens.
        services.AddHostedService<ExpiredRefreshTokenCleanup>();

        return services;
    }
}
