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
    /// Adds JWT options (validated at startup), the password hasher, the access token service and
    /// the system clock.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services)
    {
        // TryAdd: tests can register a FakeTimeProvider first and it won't be overwritten.
        services.TryAddSingleton(TimeProvider.System);

        // BindConfiguration reads the "Jwt" section lazily (when the options are first used), so
        // configuration added by WebApplicationFactory in tests is visible.
        // ValidateDataAnnotations checks the [Required]/[MinLength]/[Range] attributes and
        // ValidateOnStart runs that check while the app starts, not on the first request.
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Both services are stateless and thread-safe, so one instance for the whole app is enough.
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<IAccessTokenService, AccessTokenService>();

        return services;
    }
}
