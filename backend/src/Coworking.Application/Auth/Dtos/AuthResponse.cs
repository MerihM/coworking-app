namespace Coworking.Application.Auth.Dtos;

/// <summary>
/// JSON body returned by register, login and refresh. The refresh token is NOT in here: it travels
/// only in the httpOnly cookie, where JavaScript can't read it.
/// </summary>
/// <param name="AccessToken">JWT for the <c>Authorization: Bearer</c> header. The frontend keeps it in memory only.</param>
/// <param name="AccessTokenExpiresAt">When the access token expires (UTC); the frontend refreshes shortly before.</param>
/// <param name="User">Who is logged in.</param>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserResponse User
);
