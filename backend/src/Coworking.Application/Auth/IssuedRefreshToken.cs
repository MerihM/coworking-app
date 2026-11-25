namespace Coworking.Application.Auth;

/// <summary>
/// A newly issued refresh token, as it goes into the cookie.
/// </summary>
/// <param name="Token">
/// The RAW token (43 Base64Url characters). This is the only moment it exists in plain form:
/// it is written into the httpOnly cookie and never stored or logged.
/// </param>
/// <param name="ExpiresAt">When it expires; used as the cookie's Expires attribute.</param>
public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAt);
