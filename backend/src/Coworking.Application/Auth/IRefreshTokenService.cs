namespace Coworking.Application.Auth;

/// <summary>
/// Issues, rotates and revokes refresh tokens (CW-10, ADR 0003). Used by the auth endpoints
/// (CW-11) and by staff management (CW-12: deactivation and password reset end all sessions).
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Starts a new token family for a user who just logged in or registered.</summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The raw token for the cookie and its expiry.</returns>
    Task<IssuedRefreshToken> IssueAsync(AuthUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a valid token for a new one (rotation). Detects reuse of an already rotated
    /// token and then revokes the whole family.
    /// </summary>
    /// <param name="rawToken">The token from the cookie (may be null when the cookie is missing).</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>The new token and its user, or the failure reason.</returns>
    Task<RefreshTokenRotationResult> RotateAsync(
        string? rawToken,
        CancellationToken cancellationToken = default
    );

    /// <summary>Logout: revokes the token's whole family. Unknown or malformed tokens are ignored.</summary>
    /// <param name="rawToken">The token from the cookie.</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    Task RevokeAsync(string? rawToken, CancellationToken cancellationToken = default);

    /// <summary>Ends every session of a user (deactivation, password reset).</summary>
    /// <param name="userType">Which table the user is in.</param>
    /// <param name="userId">The user's id in that table.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>How many tokens were revoked.</returns>
    Task<int> RevokeAllForUserAsync(
        UserType userType,
        int userId,
        CancellationToken cancellationToken = default
    );
}
