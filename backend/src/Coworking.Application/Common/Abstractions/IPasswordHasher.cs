namespace Coworking.Application.Common.Abstractions;

/// <summary>
/// Turns passwords into one-way hashes and checks passwords against them (NFR01: passwords are
/// stored only in hashed form).
/// </summary>
/// <remarks>
/// The Application layer only knows this interface. The real algorithm (PBKDF2 from ASP.NET Core
/// Identity) lives in Infrastructure, so it can be replaced without touching business code.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>Creates a salted hash of the password. Two calls with the same password give different hashes.</summary>
    /// <param name="password">The plain-text password. Must not be empty.</param>
    /// <returns>A self-describing hash string (algorithm, settings, salt and hash) safe to store in the database.</returns>
    string Hash(string password);

    /// <summary>Checks a plain-text password against a stored hash.</summary>
    /// <param name="passwordHash">The hash stored in the database.</param>
    /// <param name="password">The password the user typed.</param>
    /// <returns>Whether the password matches, and whether the hash should be upgraded.</returns>
    PasswordVerificationOutcome Verify(string passwordHash, string password);
}
