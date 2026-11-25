using Coworking.Application.Auth.Dtos;
using Coworking.Application.Common;
using Coworking.Application.Common.Abstractions;
using Coworking.Application.Common.Exceptions;
using Coworking.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Auth;

/// <summary>
/// Implements <see cref="IAuthService"/>: the business logic of registration and login.
/// The controller only translates HTTP (body, cookie) to these calls and back.
/// </summary>
/// <param name="db">Members and employees.</param>
/// <param name="passwordHasher">Hashes and verifies passwords (PBKDF2).</param>
/// <param name="accessTokens">Creates the 15-minute JWT.</param>
/// <param name="refreshTokens">Creates, rotates and revokes refresh tokens (CW-10).</param>
/// <param name="currentUser">Who is calling (for <see cref="GetCurrentUserAsync"/>).</param>
/// <param name="registerValidator">Input rules for registration.</param>
/// <param name="loginValidator">Input rules for login.</param>
/// <param name="timeProvider">Source of "now" (registration time).</param>
public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokens,
    IRefreshTokenService refreshTokens,
    ICurrentUser currentUser,
    IValidator<RegisterMemberRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    TimeProvider timeProvider
) : IAuthService
{
    /// <summary>
    /// A real PBKDF2 hash (Identity v3 format, 100 000 iterations) of a random password nobody
    /// knows. When the e-mail doesn't exist, we still verify the typed password against this hash,
    /// so a wrong e-mail takes as long as a wrong password. Otherwise an attacker could tell
    /// existing accounts from non-existing ones just by measuring response times.
    /// </summary>
    private const string DummyPasswordHash =
        "AQAAAAIAAYagAAAAEBw8VvIv3vvSNLuQVYR+eImXtFvD1Jby/gVceL0F/EYx9uSC+k5dGXVNR7CXTQt4aA==";

    /// <inheritdoc />
    public async Task<AuthResult> RegisterMemberAsync(
        RegisterMemberRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        // Throws FluentValidation.ValidationException → 400 with "errors" per field.
        await registerValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = EmailAddresses.Normalize(request.Email);
        if (await db.IsEmailTakenAsync(email, cancellationToken: cancellationToken))
        {
            throw new ConflictException(
                "An account with this e-mail address already exists.",
                code: AuthErrorCodes.EmailTaken
            );
        }

        var member = new Member(
            request.FirstName,
            request.LastName,
            email,
            request.Phone,
            passwordHasher.Hash(request.Password),
            timeProvider.GetUtcNow()
        );

        db.Members.Add(member);

        // Two registrations with the same e-mail at the same moment could both pass the check
        // above; the unique index on members.email then rejects the second one, and
        // GlobalExceptionHandler turns that into 409 as well.
        await db.SaveChangesAsync(cancellationToken);

        return await StartSessionAsync(AuthUser.FromMember(member), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = EmailAddresses.Normalize(request.Email);

        // E-mails are unique across both tables, so at most one of these finds someone.
        var employee = await db.Employees.SingleOrDefaultAsync(
            e => e.Email == email,
            cancellationToken
        );
        if (employee is not null)
        {
            // The password is checked even for a deactivated account, so both cases take the
            // same time and return the same answer.
            var passwordOk = CheckPassword(
                employee.PasswordHash,
                request.Password,
                employee.ChangePasswordHash
            );
            if (!passwordOk || !employee.IsActive)
            {
                throw InvalidCredentials();
            }

            return await StartSessionAsync(AuthUser.FromEmployee(employee), cancellationToken);
        }

        var member = await db.Members.SingleOrDefaultAsync(
            m => m.Email == email,
            cancellationToken
        );
        if (member is not null)
        {
            var passwordOk = CheckPassword(
                member.PasswordHash,
                request.Password,
                member.ChangePasswordHash
            );
            if (!passwordOk || !member.IsActive)
            {
                throw InvalidCredentials();
            }

            return await StartSessionAsync(AuthUser.FromMember(member), cancellationToken);
        }

        // Unknown e-mail: do the same expensive work as a real check (see DummyPasswordHash).
        passwordHasher.Verify(DummyPasswordHash, request.Password);
        throw InvalidCredentials();
    }

    /// <inheritdoc />
    public async Task<AuthResult?> RefreshAsync(
        string? refreshToken,
        CancellationToken cancellationToken = default
    )
    {
        var rotation = await refreshTokens.RotateAsync(refreshToken, cancellationToken);
        if (!rotation.Succeeded)
        {
            // The reason (expired, reused, ...) is logged by RefreshTokenService; the client only
            // needs to know "log in again".
            return null;
        }

        // rotation.User was loaded fresh from the database, so the new access token has the
        // current role and name.
        var accessToken = accessTokens.Create(rotation.User);
        return new AuthResult(
            new AuthResponse(
                accessToken.Token,
                accessToken.ExpiresAt,
                UserResponse.From(rotation.User)
            ),
            rotation.Token
        );
    }

    /// <inheritdoc />
    public Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default) =>
        refreshTokens.RevokeAsync(refreshToken, cancellationToken);

    /// <inheritdoc />
    public async Task<UserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        AuthUser? user = null;

        if (currentUser.UserType == UserType.Member && currentUser.Id is int memberId)
        {
            var member = await db
                .Members.AsNoTracking()
                .SingleOrDefaultAsync(m => m.Id == memberId && m.IsActive, cancellationToken);
            user = member is null ? null : AuthUser.FromMember(member);
        }
        else if (currentUser.UserType == UserType.Employee && currentUser.Id is int employeeId)
        {
            var employee = await db
                .Employees.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == employeeId && e.IsActive, cancellationToken);
            user = employee is null ? null : AuthUser.FromEmployee(employee);
        }

        // The access token is still valid (max 15 min) but the account is gone or deactivated:
        // 401 makes the frontend try a refresh, which fails, and the user is logged out.
        return user is null
            ? throw new UnauthorizedException(
                "Your account is no longer active.",
                AuthErrorCodes.Unauthenticated
            )
            : UserResponse.From(user);
    }

    /// <summary>
    /// Saves pending changes (e.g. an upgraded password hash), then issues both tokens.
    /// </summary>
    private async Task<AuthResult> StartSessionAsync(
        AuthUser user,
        CancellationToken cancellationToken
    )
    {
        await db.SaveChangesAsync(cancellationToken);

        var refreshToken = await refreshTokens.IssueAsync(user, cancellationToken);
        var accessToken = accessTokens.Create(user);

        return new AuthResult(
            new AuthResponse(accessToken.Token, accessToken.ExpiresAt, UserResponse.From(user)),
            refreshToken
        );
    }

    /// <summary>
    /// Verifies the password. If the stored hash uses older settings, it is replaced with a new
    /// hash (saved by <see cref="StartSessionAsync"/>) – passwords get stronger without users
    /// noticing. That is the only moment we have the plain password to re-hash it.
    /// </summary>
    private bool CheckPassword(string storedHash, string password, Action<string> replaceHash)
    {
        var outcome = passwordHasher.Verify(storedHash, password);
        if (outcome == PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            replaceHash(passwordHasher.Hash(password));
        }

        return outcome != PasswordVerificationOutcome.Failed;
    }

    private static UnauthorizedException InvalidCredentials() =>
        new("Invalid e-mail or password.", AuthErrorCodes.InvalidCredentials);
}
