namespace Coworking.Application.Common.Abstractions;

/// <summary>
/// Result of checking a password against a stored hash.
/// </summary>
public enum PasswordVerificationOutcome
{
    /// <summary>The password is wrong (or the stored hash is unreadable).</summary>
    Failed,

    /// <summary>The password is correct.</summary>
    Success,

    /// <summary>
    /// The password is correct, but the hash was made with older/weaker settings. The caller should
    /// hash the password again and save the new hash (done during login in CW-11).
    /// </summary>
    SuccessRehashNeeded,
}
