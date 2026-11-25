using Coworking.Domain.Common;

namespace Coworking.Domain.Entities;

/// <summary>
/// A refresh token that keeps a user logged in (CW-10, ADR 0003). Not in the Class diagram: it is
/// a technical record of an active login session, needed for NFR01 (secure access).
/// </summary>
/// <remarks>
/// <para>
/// The browser keeps the raw token in an httpOnly cookie. The database stores only its SHA-256
/// <see cref="TokenHash"/>, so a leaked database dump can't be used to log in as anyone.
/// </para>
/// <para>
/// Rotation: every time a token is used it is revoked and replaced by a new one in the same
/// <see cref="FamilyId"/> (one family = one login on one device). If a token that was already
/// replaced shows up again, someone has a copy of it, and the whole family is revoked
/// (reuse detection, done in RefreshTokenService).
/// </para>
/// <para>
/// Exactly one owner: either <see cref="MemberId"/> or <see cref="EmployeeId"/> is set, because
/// members and employees live in separate tables. A database CHECK constraint enforces it too.
/// </para>
/// </remarks>
public class RefreshToken : Entity
{
    /// <summary>Length of a SHA-256 hash in bytes.</summary>
    public const int HashLength = 32;

    /// <summary>Used by EF Core when loading from the database.</summary>
    private RefreshToken() { }

    private RefreshToken(
        byte[] tokenHash,
        Guid familyId,
        int? memberId,
        int? employeeId,
        DateTimeOffset createdAt,
        TimeSpan lifetime
    )
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        if (tokenHash.Length != HashLength)
        {
            throw new ArgumentException(
                $"A token hash must be exactly {HashLength} bytes (SHA-256).",
                nameof(tokenHash)
            );
        }

        if (familyId == Guid.Empty)
        {
            throw new ArgumentException("The family id must not be empty.", nameof(familyId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);

        // Copy, so the caller can't change our hash later through the same array.
        TokenHash = tokenHash.ToArray();
        FamilyId = familyId;
        MemberId = memberId;
        EmployeeId = employeeId;
        CreatedAt = createdAt.ToUniversalTime();
        ExpiresAt = CreatedAt + lifetime;
    }

    /// <summary>SHA-256 hash of the raw token (bytea, unique). The raw token is never stored.</summary>
    public byte[] TokenHash { get; private set; } = null!;

    /// <summary>All tokens created from one login share this id (one family per device/session).</summary>
    public Guid FamilyId { get; private set; }

    /// <summary>Owner when the user is a member; otherwise null.</summary>
    public int? MemberId { get; private set; }

    /// <summary>Owner when the user is an employee; otherwise null.</summary>
    public int? EmployeeId { get; private set; }

    /// <summary>When the token was issued (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// First moment the token is no longer valid (UTC). Half-open like every period in this
    /// project (ADR 0002): valid while <c>now &lt; ExpiresAt</c>.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When the token was revoked (rotation, logout, reuse, deactivation); null while usable.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Foreign key of the token that replaced this one during rotation.</summary>
    public int? ReplacedByTokenId { get; private set; }

    /// <summary>The token that replaced this one. Set in <see cref="Rotate"/>; EF Core saves both in one go.</summary>
    public RefreshToken? ReplacedBy { get; private set; }

    /// <summary><c>true</c> once the token was revoked for any reason.</summary>
    public bool IsRevoked => RevokedAt is not null;

    /// <summary>
    /// <c>true</c> when the token was revoked BECAUSE it was rotated. Presenting such a token
    /// again is the signal for reuse detection.
    /// </summary>
    public bool WasRotated => ReplacedByTokenId is not null || ReplacedBy is not null;

    /// <summary>Creates the first token of a new login for a member.</summary>
    /// <param name="memberId">Id of a saved member.</param>
    /// <param name="tokenHash">SHA-256 hash of the raw token.</param>
    /// <param name="familyId">New family id (one per login).</param>
    /// <param name="now">Current moment.</param>
    /// <param name="lifetime">How long the token stays valid.</param>
    /// <returns>The new token.</returns>
    public static RefreshToken ForMember(
        int memberId,
        byte[] tokenHash,
        Guid familyId,
        DateTimeOffset now,
        TimeSpan lifetime
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(memberId);
        return new RefreshToken(tokenHash, familyId, memberId, null, now, lifetime);
    }

    /// <summary>Creates the first token of a new login for an employee.</summary>
    /// <param name="employeeId">Id of a saved employee.</param>
    /// <param name="tokenHash">SHA-256 hash of the raw token.</param>
    /// <param name="familyId">New family id (one per login).</param>
    /// <param name="now">Current moment.</param>
    /// <param name="lifetime">How long the token stays valid.</param>
    /// <returns>The new token.</returns>
    public static RefreshToken ForEmployee(
        int employeeId,
        byte[] tokenHash,
        Guid familyId,
        DateTimeOffset now,
        TimeSpan lifetime
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(employeeId);
        return new RefreshToken(tokenHash, familyId, null, employeeId, now, lifetime);
    }

    /// <summary>Has the token reached its expiry time?</summary>
    /// <param name="now">Current moment.</param>
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>Can the token still be used: not revoked and not expired?</summary>
    /// <param name="now">Current moment.</param>
    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    /// <summary>
    /// Uses this token up and returns its successor in the same family, for the same owner.
    /// </summary>
    /// <param name="newTokenHash">Hash of the new raw token.</param>
    /// <param name="now">Current moment.</param>
    /// <param name="lifetime">Lifetime of the new token (counted from now).</param>
    /// <returns>The new token. Add it to the DbContext; this token now points to it.</returns>
    /// <exception cref="DomainException">The token is revoked or expired.</exception>
    public RefreshToken Rotate(byte[] newTokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (!IsActive(now))
        {
            throw new DomainException("Only an active refresh token can be rotated.");
        }

        var next = new RefreshToken(newTokenHash, FamilyId, MemberId, EmployeeId, now, lifetime);
        RevokedAt = now.ToUniversalTime();
        ReplacedBy = next;
        return next;
    }

    /// <summary>
    /// Revokes the token (logout). Calling it again keeps the FIRST revocation time, so the
    /// history shows when the token really stopped working.
    /// </summary>
    /// <param name="now">Current moment.</param>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now.ToUniversalTime();
}
