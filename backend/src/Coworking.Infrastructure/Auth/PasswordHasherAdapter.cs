using Coworking.Application.Common.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Coworking.Infrastructure.Auth;

/// <summary>
/// <see cref="IPasswordHasher"/> implemented with ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Why this hasher: it is maintained by Microsoft, uses PBKDF2 with HMAC-SHA512, 100 000 iterations
/// and a random 128-bit salt per password, and writes a version marker into every hash. When
/// Microsoft raises the iteration count in the future, old hashes still verify and report
/// <see cref="PasswordVerificationOutcome.SuccessRehashNeeded"/>, so we can upgrade them at login.
/// </para>
/// <para>
/// We only use this one class from Identity, not the whole ASP.NET Core Identity system (UserManager,
/// AspNetUsers tables...), because our users are the Member and Employee tables from the class diagram.
/// <c>PasswordHasher</c> is part of the ASP.NET Core shared framework that Infrastructure already references.
/// </para>
/// </remarks>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    /// <summary>
    /// <see cref="PasswordHasher{TUser}"/> asks for a user object only so that subclasses could use
    /// per-user settings. The default implementation ignores it, so one shared dummy object is enough.
    /// </summary>
    private static readonly object NoUser = new();

    private readonly PasswordHasher<object> _inner = new();

    /// <inheritdoc />
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return _inner.HashPassword(NoUser, password);
    }

    /// <inheritdoc />
    public PasswordVerificationOutcome Verify(string passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(password);

        try
        {
            return _inner.VerifyHashedPassword(NoUser, passwordHash, password) switch
            {
                PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
                _ => PasswordVerificationOutcome.Failed,
            };
        }
        catch (FormatException)
        {
            // The stored value is not valid Base64 (e.g. hand-edited row). Treat it as a wrong
            // password instead of crashing with a 500 – the user just can't log in with it.
            return PasswordVerificationOutcome.Failed;
        }
    }
}
