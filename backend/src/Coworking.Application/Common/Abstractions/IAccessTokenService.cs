using Coworking.Application.Auth;

namespace Coworking.Application.Common.Abstractions;

/// <summary>
/// Issues short-lived JWT access tokens. Validation is done by the ASP.NET Core JwtBearer
/// handler in the Api project, using the same key and settings.
/// </summary>
public interface IAccessTokenService
{
    /// <summary>Creates a signed access token for the user.</summary>
    /// <param name="user">Who the token is for.</param>
    /// <returns>The encoded token and its expiry time.</returns>
    AccessToken Create(AuthUser user);
}
