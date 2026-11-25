namespace Coworking.Application.Auth.Dtos;

/// <summary>
/// What <see cref="IAuthService"/> hands to the controller after a successful login: the JSON body
/// and, separately, the refresh token that the controller puts into the cookie.
/// </summary>
/// <param name="Response">Goes into the response body.</param>
/// <param name="RefreshToken">Goes into the <c>cw_refresh</c> cookie, never into the body.</param>
public sealed record AuthResult(AuthResponse Response, IssuedRefreshToken RefreshToken);
