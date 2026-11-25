using System.ComponentModel.DataAnnotations;

namespace Coworking.Infrastructure.Auth;

/// <summary>
/// Settings for refresh tokens, bound from the <c>RefreshTokens</c> configuration section.
/// Validated at startup like <see cref="JwtOptions"/>.
/// </summary>
public sealed class RefreshTokenOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "RefreshTokens";

    /// <summary>
    /// How many days a refresh token stays valid. Every refresh issues a new token with a new
    /// full lifetime, so a user who opens the app at least once a week stays logged in.
    /// </summary>
    [Range(1, 90)]
    public int LifetimeDays { get; set; } = 7;

    /// <summary><see cref="LifetimeDays"/> as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan Lifetime => TimeSpan.FromDays(LifetimeDays);
}
