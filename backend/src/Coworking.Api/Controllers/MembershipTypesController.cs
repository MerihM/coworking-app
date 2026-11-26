using Coworking.Application.Auth;
using Coworking.Application.Common.Pagination;
using Coworking.Application.MembershipTypes;
using Coworking.Application.MembershipTypes.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.Api.Controllers;

/// <summary>
/// The membership price list (FR10, UC12). Management is Manager only (policy on the class);
/// the public offer (<c>GET /offer</c>) is the one anonymous action, for visitors who want to
/// see prices before registering (UC03 step 2, stakeholders "Posjetioci").
/// </summary>
/// <param name="membershipTypes">The membership type use cases.</param>
[ApiController]
[Route("api/membership-types")]
[Authorize(Policy = Policies.ManagerOnly)]
public sealed class MembershipTypesController(IMembershipTypeService membershipTypes) : ControllerBase
{
    /// <summary>The public price list: active membership types, shortest first. No login needed.</summary>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The offer (may be empty).</response>
    /// <remarks>
    /// [AllowAnonymous] overrides BOTH the ManagerOnly policy on this class and the app-wide
    /// RequireAuthorization() from Program.cs (secure by default, CW-9). It is the only way an
    /// endpoint becomes public, so each one is easy to find with a search.
    /// </remarks>
    [HttpGet("offer")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<MembershipTypeOfferItem>>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<IReadOnlyList<MembershipTypeOfferItem>>> GetOffer(CancellationToken cancellationToken) =>
        Ok(await membershipTypes.GetOfferAsync(cancellationToken));

    /// <summary>Lists membership types (active and inactive), one page at a time.</summary>
    /// <param name="query">page, pageSize, search, isActive – all optional.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The requested page.</response>
    /// <response code="400">Invalid paging or filter values.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<MembershipTypeResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<MembershipTypeResponse>>> List(
        [FromQuery] ListMembershipTypesQuery query,
        CancellationToken cancellationToken
    ) => await membershipTypes.ListAsync(query, cancellationToken);

    /// <summary>Returns one membership type.</summary>
    /// <param name="id">Membership type id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The membership type.</response>
    /// <response code="404">No membership type with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<MembershipTypeResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MembershipTypeResponse>> Get(int id, CancellationToken cancellationToken) =>
        await membershipTypes.GetAsync(id, cancellationToken);

    /// <summary>Creates a new, active membership type.</summary>
    /// <param name="request">Name, duration in days and price in KM.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="201">Created; Location points to the new type.</response>
    /// <response code="400">Invalid input (e.g. price with three decimals).</response>
    /// <response code="409">Name already used (code membership_types.name_taken).</response>
    [HttpPost]
    [ProducesResponseType<MembershipTypeResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MembershipTypeResponse>> Create(
        CreateMembershipTypeRequest request,
        CancellationToken cancellationToken
    )
    {
        var created = await membershipTypes.CreateAsync(request, cancellationToken);

        // 201 + "Location: /api/membership-types/{id}".
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Changes name, duration and price. Already bought memberships are not affected.</summary>
    /// <param name="id">Membership type id.</param>
    /// <param name="request">All editable fields.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The updated type.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="404">No membership type with this id.</response>
    /// <response code="409">Name already used (code membership_types.name_taken).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<MembershipTypeResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<MembershipTypeResponse>> Update(
        int id,
        UpdateMembershipTypeRequest request,
        CancellationToken cancellationToken
    ) => await membershipTypes.UpdateAsync(id, request, cancellationToken);

    /// <summary>Stops offering the type; bought memberships stay valid.</summary>
    /// <param name="id">Membership type id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deactivated (or already inactive).</response>
    /// <response code="404">No membership type with this id.</response>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await membershipTypes.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Offers a deactivated type again.</summary>
    /// <param name="id">Membership type id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Active.</response>
    /// <response code="404">No membership type with this id.</response>
    [HttpPost("{id:int}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        await membershipTypes.ActivateAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Deletes a type nobody has bought (otherwise: deactivate it).</summary>
    /// <param name="id">Membership type id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="404">No membership type with this id.</response>
    /// <response code="409">Memberships of this type exist (code conflict.in_use).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await membershipTypes.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
