using Coworking.Api.RateLimiting;
using Coworking.Application.Availability;
using Coworking.Application.Availability.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coworking.Api.Controllers;

/// <summary>
/// Availability search (FR02, UC02): which workspaces are free on a date between two times.
/// Public – visitors may look before registering – and therefore rate-limited per IP address.
/// </summary>
/// <param name="availability">The availability use case.</param>
[ApiController]
[Route("api/availability")]
public sealed class AvailabilityController(IAvailabilityService availability) : ControllerBase
{
    /// <summary>Lists the free workspaces for a local date and time, grouped by room.</summary>
    /// <param name="query">
    /// date (yyyy-MM-dd), from and to (HH:mm, quarter hours) in Sarajevo local time; optional type
    /// (HotDesk, Office, MeetingRoom) and minCapacity.
    /// </param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">Free workspaces (rooms without free workspaces are left out).</response>
    /// <response code="400">Missing or malformed values, more than 90 days ahead, or type FixedDesk.</response>
    /// <response code="422">Outside working hours or shorter than 1 hour (ruleCode BR04), or the time has passed (code availability.in_past).</response>
    /// <response code="429">Too many searches from this address (code auth.too_many_requests).</response>
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.PublicPolicy)]
    [ProducesResponseType<AvailabilityResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<AvailabilityResponse>> Search(
        [FromQuery] AvailabilityQuery query,
        CancellationToken cancellationToken
    ) => await availability.SearchAsync(query, cancellationToken);
}
