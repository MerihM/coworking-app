using Coworking.Application.Auth;
using Coworking.Domain.Entities;
using Coworking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Coworking.Infrastructure.Auth;

/// <summary>
/// <see cref="IRefreshTokenService"/> on PostgreSQL: rotation, reuse detection and revocation.
/// </summary>
/// <remarks>
/// Security rule for this class: the RAW token is never logged and never stored. Logs only
/// contain the family id and the owner id.
/// </remarks>
/// <param name="db">Database session of the current request (scoped).</param>
/// <param name="timeProvider">Source of "now" (fake clock in tests).</param>
/// <param name="options">Refresh token lifetime.</param>
/// <param name="logger">Logs reuse detection (Warning) and other revocations.</param>
public sealed class RefreshTokenService(
    CoworkingDbContext db,
    TimeProvider timeProvider,
    IOptions<RefreshTokenOptions> options,
    ILogger<RefreshTokenService> logger
) : IRefreshTokenService
{
    private TimeSpan Lifetime => options.Value.Lifetime;

    /// <inheritdoc />
    public async Task<IssuedRefreshToken> IssueAsync(
        AuthUser user,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = timeProvider.GetUtcNow();
        var (rawToken, hash) = RefreshTokenCrypto.Generate();

        // Version 7 GUIDs start with a timestamp, so new values are roughly sorted. That keeps the
        // family_id index compact (random v4 GUIDs scatter inserts all over the index).
        var familyId = Guid.CreateVersion7(now);

        var token = user.UserType switch
        {
            UserType.Member => RefreshToken.ForMember(user.Id, hash, familyId, now, Lifetime),
            UserType.Employee => RefreshToken.ForEmployee(user.Id, hash, familyId, now, Lifetime),
            _ => throw new ArgumentOutOfRangeException(nameof(user), user.UserType, "Unknown user type."),
        };

        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);

        return new IssuedRefreshToken(rawToken, token.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<RefreshTokenRotationResult> RotateAsync(
        string? rawToken,
        CancellationToken cancellationToken = default
    )
    {
        // Malformed input never reaches the database.
        if (!RefreshTokenCrypto.TryHash(rawToken, out var hash))
        {
            return RefreshTokenRotationResult.Fail(RefreshTokenFailure.NotFound);
        }

        var now = timeProvider.GetUtcNow();

        // Everything below runs in one transaction. If we return early without CommitAsync,
        // disposing the transaction rolls it back (nothing was changed anyway).
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // SELECT ... FOR UPDATE locks the row until the transaction ends. If two requests try to
        // rotate the same token at the same moment, the second one WAITS here, then sees the token
        // already rotated and is treated as reuse. Without the lock both could read "active" and
        // both get a new token – two valid sessions from one cookie.
        // FromSql turns {hash} into a real SQL parameter (no SQL injection possible).
        // ToListAsync + SingleOrDefault (instead of SingleOrDefaultAsync) keeps EF from wrapping
        // our SQL in a sub-query, so the FOR UPDATE stays exactly as written.
        var token = (
            await db
                .RefreshTokens.FromSql(
                    $"SELECT * FROM refresh_tokens WHERE token_hash = {hash} FOR UPDATE"
                )
                .ToListAsync(cancellationToken)
        ).SingleOrDefault();

        if (token is null)
        {
            return RefreshTokenRotationResult.Fail(RefreshTokenFailure.NotFound);
        }

        if (token.IsRevoked)
        {
            if (!token.WasRotated)
            {
                // Revoked by logout or deactivation: just refuse.
                return RefreshTokenRotationResult.Fail(RefreshTokenFailure.Revoked);
            }

            // Already rotated and presented again → a copy of the cookie exists somewhere.
            // We can't tell the real user from the attacker, so we end the whole session for both.
            var revoked = await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogWarning(
                "Refresh token reuse detected in family {FamilyId} (member {MemberId}, employee {EmployeeId}); revoked {RevokedCount} token(s)",
                token.FamilyId,
                token.MemberId,
                token.EmployeeId,
                revoked
            );
            return RefreshTokenRotationResult.Fail(RefreshTokenFailure.ReuseDetected);
        }

        if (token.IsExpired(now))
        {
            return RefreshTokenRotationResult.Fail(RefreshTokenFailure.Expired);
        }

        // Load the owner fresh from the database: a role change or deactivation since the last
        // login must be reflected in the next access token.
        var user = await LoadActiveOwnerAsync(token, cancellationToken);
        if (user is null)
        {
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Refresh refused for inactive user (member {MemberId}, employee {EmployeeId}); family {FamilyId} revoked",
                token.MemberId,
                token.EmployeeId,
                token.FamilyId
            );
            return RefreshTokenRotationResult.Fail(RefreshTokenFailure.UserInactive);
        }

        var (newRawToken, newHash) = RefreshTokenCrypto.Generate();
        var next = token.Rotate(newHash, now, Lifetime);
        db.RefreshTokens.Add(next);

        // One SaveChanges: EF inserts the new token first, then updates the old one with
        // revoked_at and replaced_by_token_id (it knows the order from the relationship).
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return RefreshTokenRotationResult.Success(
            user,
            new IssuedRefreshToken(newRawToken, next.ExpiresAt)
        );
    }

    /// <inheritdoc />
    public async Task RevokeAsync(string? rawToken, CancellationToken cancellationToken = default)
    {
        if (!RefreshTokenCrypto.TryHash(rawToken, out var hash))
        {
            return;
        }

        var familyId = await db
            .RefreshTokens.Where(t => t.TokenHash == hash)
            .Select(t => (Guid?)t.FamilyId)
            .SingleOrDefaultAsync(cancellationToken);

        if (familyId is not null)
        {
            // The whole family, not just this token: logout ends this device's session completely,
            // even if an older token of the same session is still lying around somewhere.
            await RevokeFamilyAsync(familyId.Value, timeProvider.GetUtcNow(), cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<int> RevokeAllForUserAsync(
        UserType userType,
        int userId,
        CancellationToken cancellationToken = default
    )
    {
        var now = timeProvider.GetUtcNow();
        var active = db.RefreshTokens.Where(t => t.RevokedAt == null);

        active = userType switch
        {
            UserType.Member => active.Where(t => t.MemberId == userId),
            UserType.Employee => active.Where(t => t.EmployeeId == userId),
            _ => throw new ArgumentOutOfRangeException(nameof(userType), userType, "Unknown user type."),
        };

        // ExecuteUpdateAsync runs ONE "UPDATE ... SET revoked_at = ... WHERE ..." directly in the
        // database, without loading the rows into memory first.
        return active.ExecuteUpdateAsync(
            setters => setters.SetProperty(t => t.RevokedAt, (DateTimeOffset?)now),
            cancellationToken
        );
    }

    /// <summary>Revokes every still-active token of a family. Returns how many were revoked.</summary>
    private Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        CancellationToken cancellationToken
    ) =>
        db
            .RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(t => t.RevokedAt, (DateTimeOffset?)now),
                cancellationToken
            );

    /// <summary>Loads the token's owner; null when it no longer exists or is deactivated.</summary>
    private async Task<AuthUser?> LoadActiveOwnerAsync(
        RefreshToken token,
        CancellationToken cancellationToken
    )
    {
        // AsNoTracking: we only read the user, so EF doesn't need to watch it for changes.
        if (token.MemberId is int memberId)
        {
            var member = await db
                .Members.AsNoTracking()
                .SingleOrDefaultAsync(m => m.Id == memberId, cancellationToken);
            return member is { IsActive: true } ? AuthUser.FromMember(member) : null;
        }

        if (token.EmployeeId is int employeeId)
        {
            var employee = await db
                .Employees.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == employeeId, cancellationToken);
            return employee is { IsActive: true } ? AuthUser.FromEmployee(employee) : null;
        }

        return null;
    }
}
