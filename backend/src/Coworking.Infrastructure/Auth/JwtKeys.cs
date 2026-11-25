using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Coworking.Infrastructure.Auth;

/// <summary>
/// Creates the signing key object from the configured secret.
/// </summary>
/// <remarks>
/// Token creation (<see cref="AccessTokenService"/>) and token validation (JwtBearer in the Api
/// project) must turn the secret into bytes in exactly the same way, so both use this one method.
/// </remarks>
public static class JwtKeys
{
    /// <summary>Wraps the UTF-8 bytes of the secret in a symmetric (HMAC) key.</summary>
    /// <param name="signingKey">The secret from <see cref="JwtOptions.SigningKey"/>.</param>
    /// <returns>A key usable for HMAC-SHA256 signing and validation.</returns>
    public static SymmetricSecurityKey CreateSigningKey(string signingKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(signingKey);
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }
}
