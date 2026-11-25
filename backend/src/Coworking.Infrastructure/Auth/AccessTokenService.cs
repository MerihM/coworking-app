using System.Globalization;
using Coworking.Application.Auth;
using Coworking.Application.Common.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Coworking.Infrastructure.Auth;

/// <summary>
/// Creates signed JWT access tokens (HMAC-SHA256).
/// </summary>
/// <remarks>
/// A JWT has three Base64Url parts: header (algorithm), payload (claims) and signature. The payload
/// is only encoded, NOT encrypted – anyone can read it (try jwt.io). That is why we put no secrets
/// in it, only the user's ID, type, role, e-mail and name. The signature proves the API created it.
/// </remarks>
/// <param name="options">JWT settings (issuer, audience, key, lifetime).</param>
/// <param name="timeProvider">
/// Source of "now". Injected instead of calling <c>DateTimeOffset.UtcNow</c> so tests can use a
/// fake clock and check exact expiry times.
/// </param>
public sealed class AccessTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenService
{
    /// <summary>The handler is thread-safe, so one instance serves every request.</summary>
    private readonly JsonWebTokenHandler _handler = new();

    /// <inheritdoc />
    public AccessToken Create(AuthUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var jwt = options.Value;

        // JWT time claims (iat, nbf, exp) are whole seconds since 1970. We cut off the milliseconds
        // here so the ExpiresAt we return matches the token's 'exp' claim exactly.
        var now = DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());
        var expiresAt = now.AddMinutes(jwt.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                JwtKeys.CreateSigningKey(jwt.SigningKey),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [AuthClaimTypes.Subject] = user.Id.ToString(CultureInfo.InvariantCulture),
                [AuthClaimTypes.UserType] = UserTypeClaimValues.From(user.UserType),
                [AuthClaimTypes.Role] = user.Role,
                [AuthClaimTypes.Email] = user.Email,
                [AuthClaimTypes.Name] = user.FullName,
                [AuthClaimTypes.TokenId] = Guid.NewGuid().ToString("N"),
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
