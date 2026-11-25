using System.Globalization;
using System.Threading.RateLimiting;
using Coworking.Application.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Coworking.Api.RateLimiting;

/// <summary>
/// Brute-force protection for login and registration (NFR01), using ASP.NET Core's built-in
/// rate limiter. Endpoints opt in with <c>[EnableRateLimiting(RateLimitingSetup.AuthPolicy)]</c>.
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
