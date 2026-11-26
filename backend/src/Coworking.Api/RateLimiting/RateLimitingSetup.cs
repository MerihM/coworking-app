using System.Globalization;
using System.Threading.RateLimiting;
using Coworking.Application.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Coworking.Api.RateLimiting;

/// <summary>
/// Rate limiting with ASP.NET Core's built-in limiter. Two policies, endpoints opt in with
/// <c>[EnableRateLimiting(...)]</c>:
/// <list type="bullet">
/// <item><see cref="AuthPolicy"/> – brute-force protection for login and registration (NFR01).</item>
/// <item><see cref="PublicPolicy"/> – a looser limit for anonymous read endpoints (availability search, CW-21).</item>
/// </list>
/// Both use their own counter per IP address, so searching never uses up login attempts.
/// </summary>
/// <remarks>
/// <para>
/// Fixed window: each IP address gets <see cref="RateLimitingOptions.AuthPermitLimit"/> requests per
/// window; request number 6 within the same minute gets 429 Too Many Requests with a
/// <c>Retry-After</c> header. Combined with PBKDF2 (slow hashing) this makes password guessing
/// through the API impractical.
/// </para>
/// <para>
/// Sprint 6 note: behind Nginx every request would come from Nginx's IP address, so all users
/// would share one limit. The Docker setup must add ForwardedHeaders middleware so the real
/// client IP (X-Forwarded-For) is used.
/// </para>
/// </remarks>
public static class RateLimitingSetup
{
    /// <summary>Policy name for login and registration.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Policy name for anonymous read endpoints (CW-21).</summary>
    public const string PublicPolicy = "public";

    /// <summary>Registers the options and the rate limiter policies.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services
            .AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // A "partitioned" limiter: one separate counter per IP address. The options are read
            // from DI per request (lazily), so values from test configuration are used too; the
            // limiter for an address is created once and then reused.
            options.AddPolicy(
                AuthPolicy,
                httpContext =>
                {
                    var settings = httpContext
                        .RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>()
                        .Value;

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.AuthPermitLimit,
                            Window = TimeSpan.FromSeconds(settings.AuthWindowSeconds),
                            // No queue: an extra request is rejected immediately, not delayed.
                            QueueLimit = 0,
                        }
                    );
                }
            );

            // Same shape for the public endpoints, with their own (higher) limit. A separate
            // policy = a separate set of counters: 60 searches don't block a login, and vice versa.
            options.AddPolicy(
                PublicPolicy,
                httpContext =>
                {
                    var settings = httpContext
                        .RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>()
                        .Value;

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.PublicPermitLimit,
                            Window = TimeSpan.FromSeconds(settings.PublicWindowSeconds),
                            QueueLimit = 0,
                        }
                    );
                }
            );

            // One 429 writer for both policies. The code stays "auth.too_many_requests" (already
            // translated in the frontend); its text "too many attempts, wait a moment" fits both.
            options.OnRejected = WriteTooManyRequestsAsync;
        });

        return services;
    }

    /// <summary>Writes the 429 response as ProblemDetails with Retry-After and a translatable code.</summary>
    private static async ValueTask WriteTooManyRequestsAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken
    )
    {
        var httpContext = context.HttpContext;

        // The fixed-window limiter knows when the window resets; tell the client in seconds.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(
                CultureInfo.InvariantCulture
            );
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "Too many attempts. Please wait a moment and try again.",
        };
        problem.Extensions["code"] = AuthErrorCodes.TooManyRequests;

        await httpContext
            .RequestServices.GetRequiredService<IProblemDetailsService>()
            .TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }
}
