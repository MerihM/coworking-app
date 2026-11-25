using System.Security.Cryptography;
using System.Text;
using Coworking.Infrastructure.Auth;

namespace Coworking.UnitTests.Auth;

/// <summary>Raw token format and hashing.</summary>
public sealed class RefreshTokenCryptoTests
{
    [Fact]
    public void Generate_Returns43CharBase64UrlTokenAnd32ByteHash()
    {
        var (token, hash) = RefreshTokenCrypto.Generate();

        Assert.Equal(RefreshTokenCrypto.TokenLength, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", token); // cookie-safe: no '+', '/', '='
        Assert.Equal(32, hash.Length);
    }

    [Fact]
    public void Generate_ReturnsDifferentTokensEachTime()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => RefreshTokenCrypto.Generate().Token);

        Assert.Equal(100, tokens.Distinct().Count());
    }

    [Fact]
    public void TryHash_OwnToken_GivesSameHashAsGenerate()
    {
        var (token, hash) = RefreshTokenCrypto.Generate();

        Assert.True(RefreshTokenCrypto.TryHash(token, out var again));
        Assert.Equal(hash, again);
        Assert.Equal(SHA256.HashData(Encoding.ASCII.GetBytes(token)), again);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("this-token-is-way-too-long-to-be-one-of-ours-really")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")] // 43 chars but '+' is not Base64Url
    public void TryHash_MalformedToken_ReturnsFalse(string? token)
    {
        Assert.False(RefreshTokenCrypto.TryHash(token, out var hash));
        Assert.Null(hash);
    }
}
