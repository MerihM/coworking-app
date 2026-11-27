using Coworking.Application.Auth;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Reservations;
using Coworking.Application.Reservations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.Api.Controllers;

/// <summary>
/// Reservations at the reception: the list of all reservations (e.g. "who is coming today") and
/// cancelling on behalf of a member, also after the member's 2-hour deadline (FR05, UC07, BR03).
/// Reception does not create reservations – that is the member's use case (UC04).
/// </summary>
/// <param name="reservations">The reservation use cases.</param>
[ApiController]
[Route("api/reservations")]
[Authorize(Policy = Policies.ReceptionistOrManager)]
public sealed class ReservationsController(IReservationService reservations) : ControllerBase
{
    /// <summary>Lists reservations, soonest start first, one page at a time.</summary>
    /// <param name="query">page, pageSize, date, status, search, memberId – all optional.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The requested page.</response>
    /// <response code="400">Invalid paging or filter values.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<ReservationResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<ReservationResponse>>> List(
        [FromQuery] ListReservationsQuery query,
        CancellationToken cancellationToken
    ) => await reservations.ListAsync(query, cancellationToken);

    /// <summary>Returns one reservation.</summary>
    /// <param name="id">Reservation id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The reservation.</response>
    /// <response code="404">No reservation with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ReservationResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ReservationResponse>> Get(int id, CancellationToken cancellationToken) =>
        await reservations.GetAsync(id, cancellationToken);

    /// <summary>Cancels a reservation on behalf of the member – any time before it ends (BR03).</summary>
    /// <param name="id">Reservation id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Cancelled; the workspace is free again.</response>
    /// <response code="404">No reservation with this id.</response>
    /// <response code="409">Already cancelled or ended (reservations.not_cancellable).</response>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await reservations.CancelAsync(id, cancellationToken);
        return NoContent();
    }
}
