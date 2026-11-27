using Coworking.Application.Auth;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Memberships;
using Coworking.Application.Memberships.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.Api.Controllers;

/// <summary>
/// Memberships at the reception (FR07, UC09): the list (e.g. "waiting for payment"), recording a
/// payment and deleting an unpaid order. The member's own endpoints are in
/// <see cref="MyMembershipsController"/> – a separate controller, because policies on a class and
/// on an action are COMBINED (both must pass), so one class can't serve both roles cleanly.
/// </summary>
/// <param name="memberships">The membership use cases.</param>
[ApiController]
[Route("api/memberships")]
[Authorize(Policy = Policies.ReceptionistOrManager)]
public sealed class MembershipsController(IMembershipService memberships) : ControllerBase
{
    /// <summary>Lists memberships, one page at a time.</summary>
    /// <param name="query">page, pageSize, memberId, status (Unpaid/Active/Expired), search – all optional.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The requested page (Unpaid: oldest first; otherwise newest first).</response>
    /// <response code="400">Invalid paging or filter values.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<MembershipResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<MembershipResponse>>> List(
        [FromQuery] ListMembershipsQuery query,
        CancellationToken cancellationToken
    ) => await memberships.ListAsync(query, cancellationToken);

    /// <summary>Returns one membership with its payment.</summary>
    /// <param name="id">Membership id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The membership.</response>
    /// <response code="404">No membership with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MembershipResponse>> Get(int id, CancellationToken cancellationToken) =>
        await memberships.GetAsync(id, cancellationToken);

    /// <summary>
    /// Records the payment and activates the membership (BR05): it starts today, or – for a
    /// renewal – on the day the current membership ends.
    /// </summary>
    /// <param name="id">Membership id.</param>
    /// <param name="request">Amount (must equal the price) and method (Cash/Card).</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The active membership with its period and payment.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="404">No membership with this id.</response>
    /// <response code="409">Already paid (memberships.already_paid).</response>
    /// <response code="422">Amount differs from the price (ruleCode BR05).</response>
    [HttpPost("{id:int}/payment")]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<MembershipResponse>> RecordPayment(
        int id,
        RecordPaymentRequest request,
        CancellationToken cancellationToken
    ) => await memberships.RecordPaymentAsync(id, request, cancellationToken);

    /// <summary>Deletes an unpaid membership.</summary>
    /// <param name="id">Membership id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="404">No membership with this id.</response>
    /// <response code="422">The membership is paid (memberships.already_paid).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await memberships.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
