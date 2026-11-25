using Coworking.Application.Auth.Dtos;

namespace Coworking.Application.Auth;

/// <summary>
/// Use cases behind <c>/api/auth</c>: register (FR01, UC01), login, refresh, logout and "who am I".
/// </summary>
public interface IAuthService
{
    /// <summary>Creates a member account and logs the new member in.</summary>
    /// <param name="request">Registration form.</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>Access token + user for the body, refresh token for the cookie.</returns>
    /// <exception cref="FluentValidation.ValidationException">Invalid input (400).</exception>
    /// <exception cref="Coworking.Application.Common.Exceptions.ConflictException">E-mail already used (409, auth.email_taken).</exception>
    Task<AuthResult> RegisterMemberAsync(
        RegisterMemberRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>Checks e-mail + password of a member or employee and starts a session.</summary>
    /// <param name="request">Login form.</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>Access token + user for the body, refresh token for the cookie.</returns>
    /// <exception cref="FluentValidation.ValidationException">Missing input (400).</exception>
    /// <exception cref="Coworking.Application.Common.Exceptions.UnauthorizedException">Wrong credentials or inactive account (401).</exception>
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges the refresh cookie for a new access token and a new refresh token (rotation).
    /// </summary>
    /// <param name="refreshToken">Value of the refresh cookie (null when missing).</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>The new session, or <c>null</c> when the user must log in again.</returns>
    Task<AuthResult?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Ends the session that the refresh cookie belongs to. Never fails.</summary>
    /// <param name="refreshToken">Value of the refresh cookie (null when missing).</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Loads the logged-in user fresh from the database.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The current user.</returns>
    /// <exception cref="Coworking.Application.Common.Exceptions.UnauthorizedException">The account no longer exists or was deactivated (401).</exception>
    Task<UserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
