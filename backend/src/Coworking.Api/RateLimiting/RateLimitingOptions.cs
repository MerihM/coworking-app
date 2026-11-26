using System.ComponentModel.DataAnnotations;

namespace Coworking.Api.RateLimiting;

/// <summary>
/// Rate limits, bound from the <c>RateLimiting</c> configuration section.
/// Auth (login, register): 5 attempts per minute per IP address (Sprint 2).
/// Public (anonymous read endpoints such as availability search, CW-21): 60 per minute per IP.
/// Integration tests raise both limits, because all their requests come from the same (empty) address.
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

    /// <summary>
    /// How many requests one IP address may send to a public endpoint per window (CW-21).
    /// Generous for a person clicking through a search form; low enough that a script can't
    /// hammer the database with expensive availability queries.
    /// </summary>
    [Range(1, 100_000)]
    public int PublicPermitLimit { get; set; } = 60;

    /// <summary>Length of the public window in seconds.</summary>
    [Range(1, 3600)]
    public int PublicWindowSeconds { get; set; } = 60;
}
