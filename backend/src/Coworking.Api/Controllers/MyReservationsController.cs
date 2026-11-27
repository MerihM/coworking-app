using Coworking.Application.Auth;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Reservations;
using Coworking.Application.Reservations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.Api.Controllers;

/// <summary>
/// The logged-in member's reservations: reserve (FR04, UC04), alternatives when the slot is taken
/// (UC06), list (FR06, UC08) and cancel (FR05, UC07). Member only; the member id always comes from
/// the token, never from the URL. Reception's endpoints are in <see cref="ReservationsController"/>
/// (class + action policies are combined, so one class can't serve both roles).
/// </summary>
/// <param name="reservations">The reservation use cases.</param>
[ApiController]
[Route("api/reservations/mine")]
[Authorize(Policy = Policies.MemberOnly)]
public sealed class MyReservationsController(IReservationService reservations) : ControllerBase
{
    /// <summary>My reservations: upcoming (soonest first) or past (newest first).</summary>
    /// <param name="query">scope (Upcoming/Past), page, pageSize – all optional.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The requested page.</response>
    /// <response code="400">Invalid paging or scope.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<ReservationResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<ReservationResponse>>> List(
        [FromQuery] MyReservationsQuery query,
        CancellationToken cancellationToken
    ) => await reservations.ListMineAsync(query, cancellationToken);

    /// <summary>
    /// Reserves a workspace (UC04). Checks: past → working hours (BR04) → active membership on that
    /// date (BR02) → workspace free (BR01).
    /// </summary>
    /// <param name="request">Workspace and local date/times.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="201">The confirmed reservation.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="404">Unknown workspace.</response>
    /// <response code="409">The workspace is taken (reservations.slot_taken, ruleCode BR01) – ask for alternatives.</response>
    /// <response code="422">
    /// reservations.workspace_unavailable, reservations.in_past, or ruleCode BR04 (working hours) /
    /// BR02 (no active membership – offer buying one).
    /// </response>
    [HttpPost]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<ReservationResponse>> Create(
        CreateReservationRequest request,
        CancellationToken cancellationToken
    )
    {
        var created = await reservations.CreateMineAsync(request, cancellationToken);
        // 201 without a Location header: /api/reservations/{id} is a reception endpoint (same as memberships).
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>
    /// UC06: what to offer when a slot is taken – other free workspaces of the same type at the same
    /// time, and the next free slots of the same workspace.
    /// </summary>
    /// <param name="query">workspaceId, date, from, to of the slot that was taken.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The offers (lists may be empty).</response>
    /// <response code="400">Missing or invalid parameters.</response>
    /// <response code="404">Unknown workspace.</response>
    /// <response code="422">Workspace not bookable, or the period breaks BR04.</response>
    [HttpGet("~/api/reservations/alternatives")]
    [ProducesResponseType<ReservationAlternativesResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<ReservationAlternativesResponse>> Alternatives(
        [FromQuery] ReservationAlternativesQuery query,
        CancellationToken cancellationToken
    ) => await reservations.GetAlternativesAsync(query, cancellationToken);

    /// <summary>Cancels my reservation – at the latest 2 hours before it starts (BR03).</summary>
    /// <param name="id">Reservation id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Cancelled; the workspace is free again.</response>
    /// <response code="404">No such reservation of mine.</response>
    /// <response code="409">Already cancelled or ended (reservations.not_cancellable).</response>
    /// <response code="422">Less than 2 hours before the start (ruleCode BR03) – the reception can still cancel it.</response>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await reservations.CancelMineAsync(id, cancellationToken);
        return NoContent();
    }
}
