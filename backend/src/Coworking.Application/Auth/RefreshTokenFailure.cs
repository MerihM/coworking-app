namespace Coworking.Application.Auth;

/// <summary>
/// Why a refresh token could not be used. The API answers 401 for all of them (CW-11) – the
/// reason is for logs and tests, the client must not learn more than "log in again".
/// </summary>
public enum RefreshTokenFailure
{
    /// <summary>No token, a malformed token, or a token that doesn't exist in the database.</summary>
    NotFound,

    /// <summary>The token reached its expiry time.</summary>
    Expired,

    /// <summary>The token was revoked (logout, family revoked, user deactivated).</summary>
    Revoked,

    /// <summary>
    /// The token had already been rotated and was presented again – someone has a copy.
    /// The whole family was revoked.
    /// </summary>
    ReuseDetected,

    /// <summary>The owner is deactivated (or deleted). The family was revoked.</summary>
    UserInactive,
}
