using Coworking.Application.Auth;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Members;
using Coworking.Application.Members.Dtos;
using Coworking.Application.Memberships;
using Coworking.Application.Memberships.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.Api.Controllers;

/// <summary>
/// Member management at the reception (FR08, UC10): list/search, create, edit (incl. the fixed
/// desk), deactivate/activate, delete, and selling a membership at the desk. Receptionist or
/// Manager (the design names the receptionist; the manager supervises reception).
/// </summary>
/// <param name="members">The member use cases.</param>
/// <param name="memberships">The membership use cases (selling at the desk).</param>
[ApiController]
[Route("api/members")]
[Authorize(Policy = Policies.ReceptionistOrManager)]
public sealed class MembersController(IMemberService members, IMembershipService memberships) : ControllerBase
{
    /// <summary>Lists members, one page at a time, sorted by last name.</summary>
    /// <param name="query">page, pageSize, search (name, e-mail, phone), isActive – all optional.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The requested page.</response>
    /// <response code="400">Invalid paging or filter values.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<MemberResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<MemberResponse>>> List(
        [FromQuery] ListMembersQuery query,
        CancellationToken cancellationToken
    ) => await members.ListAsync(query, cancellationToken);

    /// <summary>Returns one member with the fixed desk and the current membership.</summary>
    /// <param name="id">Member id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The member.</response>
    /// <response code="404">No member with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<MemberResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MemberResponse>> Get(int id, CancellationToken cancellationToken) =>
        await members.GetAsync(id, cancellationToken);

    /// <summary>Registers a member at the desk with an initial password.</summary>
    /// <param name="request">Personal data, initial password, optional fixed desk.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="201">Created; Location points to the new member.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="404">The fixed desk doesn't exist.</response>
    /// <response code="409">E-mail already used (auth.email_taken) or desk taken (members.fixed_desk_taken).</response>
    /// <response code="422">The desk is not an active fixed desk (members.fixed_desk_invalid).</response>
    [HttpPost]
    [ProducesResponseType<MemberResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<MemberResponse>> Create(CreateMemberRequest request, CancellationToken cancellationToken)
    {
        var created = await members.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Changes personal data and the fixed desk (null removes it).</summary>
    /// <param name="id">Member id.</param>
    /// <param name="request">The whole editable state.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The updated member.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="404">No member (or fixed desk) with this id.</response>
    /// <response code="409">E-mail already used or desk taken.</response>
    /// <response code="422">The desk is not an active fixed desk.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<MemberResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<MemberResponse>> Update(
        int id,
        UpdateMemberRequest request,
        CancellationToken cancellationToken
    ) => await members.UpdateAsync(id, request, cancellationToken);

    /// <summary>Deactivates the member: no login, no new memberships. Ends their sessions.</summary>
    /// <param name="id">Member id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deactivated (or already inactive).</response>
    /// <response code="404">No member with this id.</response>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await members.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Activates a deactivated member again.</summary>
    /// <param name="id">Member id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Active.</response>
    /// <response code="404">No member with this id.</response>
    [HttpPost("{id:int}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        await members.ActivateAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Deletes a member without memberships and reservations (otherwise: deactivate).</summary>
    /// <param name="id">Member id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="404">No member with this id.</response>
    /// <response code="409">The member has memberships or reservations (conflict.in_use).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await members.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Sells a membership at the desk; it is Unpaid until the payment is recorded (BR05).</summary>
    /// <param name="id">Member id.</param>
    /// <param name="request">The membership type.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="201">Created; Location points to the membership.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="404">Unknown member or membership type.</response>
    /// <response code="409">The member already has an unpaid membership (memberships.unpaid_exists).</response>
    /// <response code="422">Member deactivated (members.inactive) or type no longer offered (memberships.type_inactive).</response>
    [HttpPost("{id:int}/memberships")]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<MembershipResponse>> SellMembership(
        int id,
        BuyMembershipRequest request,
        CancellationToken cancellationToken
    )
    {
        var created = await memberships.SellAsync(id, request, cancellationToken);
        return CreatedAtAction(
            nameof(MembershipsController.Get),
            "Memberships",
            new { id = created.Id },
            created
        );
    }
}
