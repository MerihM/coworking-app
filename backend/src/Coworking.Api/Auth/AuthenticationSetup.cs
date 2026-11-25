using Coworking.Application.Auth;
using Coworking.Application.Common.Abstractions;
using Coworking.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Coworking.Api.Auth;

/// <summary>
/// Configures how the API recognizes the caller: a JWT in the <c>Authorization: Bearer</c> header.
/// </summary>
public static class AuthenticationSetup
{
    /// <summary>
    /// Adds JwtBearer authentication, ProblemDetails for 401/403, and <see cref="ICurrentUser"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        // "Bearer" becomes the default scheme: every request is checked for a bearer token.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configure the bearer options through the options system instead of inside AddJwtBearer(...).
        // This way we can depend on IOptions<JwtOptions>, which is read lazily and validated at
        // startup – the same values AccessTokenService uses to create tokens.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep claim names exactly as written in the token ("sub", "role", ...).
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtKeys.CreateSigningKey(jwt.SigningKey),

                    // Accept only the algorithm we sign with. Blocks "alg: none" and algorithm
                    // confusion attacks where an attacker picks a weaker algorithm in the header.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                    // Default is 5 minutes of tolerance for clocks that are out of sync. API and
                    // token issuer are the same server here, so 30 seconds is plenty.
                    ClockSkew = TimeSpan.FromSeconds(30),

                    // Tell ASP.NET which claims hold the name and the role, so that
                    // User.Identity.Name and [Authorize(Roles = ...)]/RequireRole work.
                    NameClaimType = AuthClaimTypes.Name,
                    RoleClaimType = AuthClaimTypes.Role,
                };

                bearer.Events = new JwtBearerEvents
                {
                    OnChallenge = WriteUnauthorizedAsync,
                    OnForbidden = WriteForbiddenAsync,
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        return services;
    }

    /// <summary>
    /// Runs when a protected endpoint is called without a valid token. By default JwtBearer returns
    /// an empty 401; we return a ProblemDetails body like every other error in this API.
    /// </summary>
    private static Task WriteUnauthorizedAsync(JwtBearerChallengeContext context)
    {
        // Skip the default behaviour (empty body); we write the whole response ourselves.
        context.HandleResponse();

        // RFC 6750: a 401 for bearer auth must say which scheme is expected. "invalid_token"
        // tells the client the token it sent was rejected (vs. no token at all).
        context.Response.Headers.WWWAuthenticate = context.AuthenticateFailure is null
            ? "Bearer"
            : "Bearer error=\"invalid_token\"";

        var detail = context.AuthenticateFailure switch
        {
            null => "Authentication is required to access this resource.",
            SecurityTokenExpiredException => "The access token has expired.",
            _ => "The access token is invalid.",
        };

        return WriteProblemAsync(
            context.HttpContext,
            StatusCodes.Status401Unauthorized,
            detail,
            AuthErrorCodes.Unauthenticated);
    }

    /// <summary>
    /// Runs when the caller is logged in but their role is not allowed (policy failed).
    /// </summary>
    private static Task WriteForbiddenAsync(ForbiddenContext context) =>
        WriteProblemAsync(
            context.HttpContext,
            StatusCodes.Status403Forbidden,
            "You do not have permission to perform this action.",
            AuthErrorCodes.Forbidden);

    /// <summary>
    /// Writes an RFC 7807 ProblemDetails response through <see cref="IProblemDetailsService"/>,
    /// so it gets the same defaults (type, title, traceId) as responses from the exception handler.
    /// </summary>
    private static async Task WriteProblemAsync(HttpContext httpContext, int status, string detail, string code)
    {
        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
        problemDetails.Extensions["code"] = code;

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        // TryWriteAsync returns false (instead of throwing) if the client asked for a format we
        // can't produce, e.g. "Accept: text/html". The status code is still correct in that case.
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
        });
    }
}
