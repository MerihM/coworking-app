using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Coworking.Infrastructure.Auth;

/// <summary>
/// Creates raw refresh tokens and their hashes.
/// </summary>
/// <remarks>
/// <para>
/// Raw token: 32 bytes (256 bits) from the operating system's cryptographic random generator,
/// written as Base64Url (A–Z a–z 0–9 - _, no padding) = 43 characters, safe in a cookie.
/// </para>
/// <para>
/// Why plain SHA-256 here, when passwords use slow PBKDF2? Slow hashing protects WEAK secrets
/// (people choose guessable passwords) against guessing. A random 256-bit token can't be guessed
/// at any speed, so a fast hash is enough – and it lets us find a token by its hash with an index.
/// </para>
/// </remarks>
public static class RefreshTokenCrypto
{
    /// <summary>Random bytes per token (256 bits).</summary>
    public const int TokenBytes = 32;

    /// <summary>Length of the Base64Url text of <see cref="TokenBytes"/> bytes without padding.</summary>
    public const int TokenLength = 43;

    /// <summary>Creates a new random token and its hash.</summary>
    /// <returns>The raw token (for the cookie) and its SHA-256 hash (for the database).</returns>
    public static (string Token, byte[] Hash) Generate()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        return (token, Hash(token));
    }

    /// <summary>Hashes a token that came back from a cookie, after checking its format.</summary>
    /// <param name="token">The raw token, possibly null or tampered with.</param>
    /// <param name="hash">The SHA-256 hash when the method returns <c>true</c>.</param>
    /// <returns><c>false</c> for anything that can't be one of our tokens (no database query needed).</returns>
    public static bool TryHash([NotNullWhen(true)] string? token, [NotNullWhen(true)] out byte[]? hash)
    {
        if (token is null || token.Length != TokenLength || !Base64Url.IsValid(token))
        {
            hash = null;
            return false;
        }

        hash = Hash(token);
        return true;
    }

    /// <summary>SHA-256 always returns 32 bytes = RefreshToken.HashLength.</summary>
    private static byte[] Hash(string token) => SHA256.HashData(Encoding.ASCII.GetBytes(token));
}
