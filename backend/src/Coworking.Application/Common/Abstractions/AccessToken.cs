namespace Coworking.Application.Common.Abstractions;

/// <summary>
/// A freshly issued JWT access token.
/// </summary>
/// <param name="Token">The encoded JWT (three Base64Url parts separated by dots).</param>
/// <param name="ExpiresAt">
/// When the token stops working (UTC). Sent to the frontend so it can refresh shortly before.
/// </param>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
