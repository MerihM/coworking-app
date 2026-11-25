using Coworking.Application.Common.Abstractions;
using Coworking.Infrastructure.Auth;

namespace Coworking.UnitTests.Auth;

/// <summary>
/// NFR01: passwords are stored only as hashes. These tests check our wrapper around
/// ASP.NET Core Identity's PasswordHasher.
/// </summary>
public sealed class PasswordHasherAdapterTests
{
    private const string Password = "Coworking123";

    private readonly PasswordHasherAdapter _hasher = new();

    [Fact]
    public void Hash_DoesNotContainThePlainPassword()
    {
        var hash = _hasher.Hash(Password);

        Assert.NotEqual(Password, hash);
        Assert.DoesNotContain(Password, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_UsesIdentityV3Format()
    {
        // Byte 0x01 = format version 3, then 0x00000002 = HMAC-SHA512 → Base64 "AQAAAAI...".
        var hash = _hasher.Hash(Password);

        Assert.StartsWith("AQAAAAI", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_SamePasswordTwice_GivesDifferentHashes()
    {
        // A random salt per hash means equal passwords don't produce equal hashes,
        // so a leaked table doesn't reveal which users share a password.
        Assert.NotEqual(_hasher.Hash(Password), _hasher.Hash(Password));
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsSuccess()
    {
        var hash = _hasher.Hash(Password);

        Assert.Equal(PasswordVerificationOutcome.Success, _hasher.Verify(hash, Password));
    }

    [Theory]
    [InlineData("coworking123")] // case matters
    [InlineData("Coworking1234")]
    [InlineData("")]
    public void Verify_WrongPassword_ReturnsFailed(string attempt)
    {
        var hash = _hasher.Hash(Password);

        Assert.Equal(PasswordVerificationOutcome.Failed, _hasher.Verify(hash, attempt));
    }

    [Fact]
    public void Verify_MalformedHash_ReturnsFailedInsteadOfThrowing()
    {
        Assert.Equal(PasswordVerificationOutcome.Failed, _hasher.Verify("not-a-valid-hash!!", Password));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Hash_EmptyPassword_Throws(string? password)
    {
        Assert.ThrowsAny<ArgumentException>(() => _hasher.Hash(password!));
    }
}
