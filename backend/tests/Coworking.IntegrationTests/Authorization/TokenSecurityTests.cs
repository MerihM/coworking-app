using System.Net;
using System.Net.Http.Headers;
using Coworking.Application.Auth;
using Coworking.Infrastructure.Auth;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Coworking.IntegrationTests.Authorization;

/// <summary>
/// NFR01: hand-crafted and tampered tokens must be rejected. (Expired, wrong-key and garbage
/// tokens are covered in AuthenticationTests, CW-9.)
/// </summary>
public sealed class TokenSecurityTests(AuthTestingApiFactory factory) : IClassFixture<AuthTestingApiFactory>
{
    private static readonly AuthUser Manager =
        new(7, UserType.Employee, Roles.Manager, "manager@example.com", "Test Manager");

    [Fact]
    public async Task NFR01_UnsignedTokenWithAlgNone_Returns401()
    {
        // The classic JWT attack: header {"alg":"none"} and no signature. Some old libraries
        // accepted such tokens as valid. Our ValidAlgorithms = [HS256] must refuse it.
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncoder.Encode(
            $$"""{"sub":"7","user_type":"employee","role":"Manager","iss":"{{TestAuthSettings.Issuer}}","aud":"{{TestAuthSettings.Audience}}","nbf":{{now}},"iat":{{now}},"exp":{{now + 600}}}"""
        );

        var response = await GetAsync("/test/policies/manager", $"{header}.{payload}.");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NFR01_TokenFromAnotherIssuer_Returns401()
    {
        var options = TestAuthSettings.CreateJwtOptions();
        options.Issuer = "some-other-api";

        var response = await GetAsync("/test/policies/manager", CreateToken(options, Manager));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NFR01_TokenForAnotherAudience_Returns401()
    {
        // Same key and issuer, but meant for a different client application.
        var options = TestAuthSettings.CreateJwtOptions();
        options.Audience = "some-other-app";

        var response = await GetAsync("/test/policies/manager", CreateToken(options, Manager));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NFR01_ValidSignatureButMissingUserType_Returns403()
    {
        // Correctly signed, but without the user_type claim. Policies check user_type AND role,
        // so a token that only says role=Manager is not enough.
        var now = DateTime.UtcNow;
        var token = new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = TestAuthSettings.Issuer,
                Audience = TestAuthSettings.Audience,
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(10),
                SigningCredentials = new SigningCredentials(
                    JwtKeys.CreateSigningKey(TestAuthSettings.SigningKey),
                    SecurityAlgorithms.HmacSha256
                ),
                Claims = new Dictionary<string, object> { ["sub"] = "7", ["role"] = Roles.Manager },
            }
        );

        var response = await GetAsync("/test/policies/manager", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NFR01_PayloadEditedAfterSigning_Returns401()
    {
        // A member decodes their own token, changes "Member" to "Manager" and re-encodes the
        // payload. The signature no longer matches the payload → rejected.
        var memberToken = factory.CreateToken(
            new AuthUser(1, UserType.Member, Roles.Member, "member@example.com", "Test Member")
        );
        var parts = memberToken.Split('.');
        var payload = Base64UrlEncoder.Decode(parts[1])
            .Replace("\"Member\"", "\"Manager\"", StringComparison.Ordinal)
            .Replace("\"member\"", "\"employee\"", StringComparison.Ordinal);
        var tampered = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";

        var response = await GetAsync("/test/policies/manager", tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string CreateToken(JwtOptions options, AuthUser user) =>
        new AccessTokenService(Options.Create(options), TimeProvider.System).Create(user).Token;

    private async Task<HttpResponseMessage> GetAsync(string url, string token)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(url);
    }
}
