using Coworking.Application.Auth;
using Coworking.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Coworking.UnitTests.Auth;

/// <summary>
/// Checks the content, lifetime and signature of the tokens we issue. A fake clock makes the
/// expiry time exact and repeatable.
/// </summary>
public sealed class AccessTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly JwtOptions Jwt = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = new string('k', 48),
        AccessTokenMinutes = 15,
    };

    private static readonly AuthUser Manager =
        new(5, UserType.Employee, Roles.Manager, "manager@example.com", "Test Manager");

    private static AccessTokenService CreateService() => new(Options.Create(Jwt), new FakeTimeProvider(Now));

    [Fact]
    public void Create_ExpiresAfterConfiguredMinutes()
    {
        var token = CreateService().Create(Manager);
        var jwt = new JsonWebToken(token.Token);

        Assert.Equal(Now.AddMinutes(15), token.ExpiresAt);
        Assert.Equal(Now.AddMinutes(15).UtcDateTime, jwt.ValidTo);
        Assert.Equal(Now.UtcDateTime, jwt.IssuedAt);
    }

    [Fact]
    public void Create_ContainsUserClaims()
    {
        var jwt = new JsonWebToken(CreateService().Create(Manager).Token);

        Assert.Equal("5", jwt.GetClaim(AuthClaimTypes.Subject).Value);
        Assert.Equal(UserTypeClaimValues.Employee, jwt.GetClaim(AuthClaimTypes.UserType).Value);
        Assert.Equal(Roles.Manager, jwt.GetClaim(AuthClaimTypes.Role).Value);
        Assert.Equal("manager@example.com", jwt.GetClaim(AuthClaimTypes.Email).Value);
        Assert.Equal("Test Manager", jwt.GetClaim(AuthClaimTypes.Name).Value);
        Assert.False(string.IsNullOrEmpty(jwt.GetClaim(AuthClaimTypes.TokenId).Value));
        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Equal("test-audience", Assert.Single(jwt.Audiences));
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Alg);
    }

    [Fact]
    public void Create_TwoTokensForSameUser_HaveDifferentIds()
    {
        var service = CreateService();

        var first = new JsonWebToken(service.Create(Manager).Token);
        var second = new JsonWebToken(service.Create(Manager).Token);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Create_SignatureIsValidWithSameKey()
    {
        var token = CreateService().Create(Manager);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, ValidationParameters(Jwt.SigningKey));

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task Create_SignatureIsRejectedWithDifferentKey()
    {
        var token = CreateService().Create(Manager);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, ValidationParameters(new string('x', 48)));

        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Lifetime is not validated here because the token was issued at a fake time in the past;
    /// lifetime validation is covered by the integration tests through the real JwtBearer handler.
    /// </summary>
    private static TokenValidationParameters ValidationParameters(string signingKey) =>
        new()
        {
            ValidIssuer = Jwt.Issuer,
            ValidAudience = Jwt.Audience,
            IssuerSigningKey = JwtKeys.CreateSigningKey(signingKey),
            ValidateLifetime = false,
        };
}
