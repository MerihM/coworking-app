using System.Diagnostics.CodeAnalysis;

namespace Coworking.Application.Auth;

/// <summary>
/// Outcome of <see cref="IRefreshTokenService.RotateAsync"/>: either a new token plus the user
/// it belongs to, or the reason it failed.
/// </summary>
/// <remarks>
/// A result object instead of exceptions, because an invalid refresh token is a normal, expected
/// situation (expired cookie after a week), not an error in our code.
/// </remarks>
public sealed class RefreshTokenRotationResult
{
    private RefreshTokenRotationResult(
        AuthUser? user,
        IssuedRefreshToken? token,
        RefreshTokenFailure? failure
    )
    {
        User = user;
        Token = token;
        Failure = failure;
    }

    /// <summary>The user the token belongs to (fresh from the database: current role and name).</summary>
    public AuthUser? User { get; }

    /// <summary>The replacement token to send back in the cookie.</summary>
    public IssuedRefreshToken? Token { get; }

    /// <summary>Why the rotation failed; null on success.</summary>
    public RefreshTokenFailure? Failure { get; }

    /// <summary>
    /// <c>true</c> on success. The attribute tells the compiler that <see cref="User"/> and
    /// <see cref="Token"/> are not null when this is true, so callers need no extra null checks.
    /// </summary>
    [MemberNotNullWhen(true, nameof(User), nameof(Token))]
    public bool Succeeded => Failure is null;

    /// <summary>Successful rotation.</summary>
    /// <param name="user">Owner of the token.</param>
    /// <param name="token">The new token.</param>
    /// <returns>A successful result.</returns>
    public static RefreshTokenRotationResult Success(AuthUser user, IssuedRefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(token);
        return new RefreshTokenRotationResult(user, token, null);
    }

    /// <summary>Failed rotation.</summary>
    /// <param name="failure">The reason.</param>
    /// <returns>A failed result.</returns>
    public static RefreshTokenRotationResult Fail(RefreshTokenFailure failure) =>
        new(null, null, failure);
}
