using System.ComponentModel.DataAnnotations;

namespace Coworking.Infrastructure.Auth;

/// <summary>
/// Settings for JWT access tokens, bound from the <c>Jwt</c> configuration section.
/// </summary>
/// <remarks>
/// Issuer, Audience and AccessTokenMinutes live in appsettings.json. The SigningKey is a secret:
/// in development it comes from <c>dotnet user-secrets</c>, in Docker (Sprint 6) from an environment
/// variable (<c>Jwt__SigningKey</c>). It must never be committed.
/// The attributes are checked when the app starts (<c>ValidateOnStart</c>), so a missing key stops
/// the app immediately with a clear message instead of failing on the first login.
/// </remarks>
public sealed class JwtOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// HMAC-SHA256 needs a key of at least 256 bits; 32 ASCII characters = 32 bytes = 256 bits.
    /// </summary>
    public const int MinimumSigningKeyLength = 32;

    /// <summary>Who created the token (the <c>iss</c> claim), e.g. <c>coworking-api</c>.</summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Who the token is meant for (the <c>aud</c> claim), e.g. <c>coworking-web</c>.</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Secret used to sign and verify tokens. Anyone who knows it can create valid tokens.</summary>
    [Required(ErrorMessage = "Jwt:SigningKey is missing. Set it with: dotnet user-secrets set \"Jwt:SigningKey\" \"$(openssl rand -base64 48)\" --project src/Coworking.Api")]
    [MinLength(MinimumSigningKeyLength, ErrorMessage = "Jwt:SigningKey must be at least 32 characters (256 bits). Generate one with: openssl rand -base64 48")]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Access token lifetime in minutes. Short on purpose: a stolen access token works only until it
    /// expires, and the refresh token (CW-10) keeps the user logged in.
    /// </summary>
    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;
}
