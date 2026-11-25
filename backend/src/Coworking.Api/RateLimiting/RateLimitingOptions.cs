using System.ComponentModel.DataAnnotations;

namespace Coworking.Api.RateLimiting;

/// <summary>
/// Limits for login and registration, bound from the <c>RateLimiting</c> configuration section.
/// Defaults: 5 attempts per minute per IP address (decided at sprint start). Integration tests
/// raise the limit, because all their requests come from the same (empty) address.
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>How many login/register requests one IP address may make per window.</summary>
    [Range(1, 100_000)]
    public int AuthPermitLimit { get; set; } = 5;

    /// <summary>Length of the window in seconds.</summary>
    [Range(1, 3600)]
    public int AuthWindowSeconds { get; set; } = 60;
}
