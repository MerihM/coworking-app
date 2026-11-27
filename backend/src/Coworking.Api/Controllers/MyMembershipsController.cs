using Coworking.Application.Auth;
using Coworking.Application.Memberships;
using Coworking.Application.Memberships.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.Api.Controllers;

/// <summary>
/// The logged-in member's memberships (FR03, UC03): history, buying from the offer, and
/// cancelling an unpaid order. Member only; the member id always comes from the token, never
/// from the URL, so a member can't reach anyone else's data.
/// </summary>
/// <param name="memberships">The membership use cases.</param>
[ApiController]
[Route("api/memberships/mine")]
[Authorize(Policy = Policies.MemberOnly)]
public sealed class MyMembershipsController(IMembershipService memberships) : ControllerBase
{
    /// <summary>My memberships, newest purchase first.</summary>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The memberships (may be empty).</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MembershipResponse>>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<IReadOnlyList<MembershipResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await memberships.ListMineAsync(cancellationToken));

    /// <summary>Buys a membership; it stays Unpaid until it is paid at the reception (UC03 steps 4–5).</summary>
    /// <param name="request">The membership type from the offer.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="201">The new, unpaid membership; its price is the amount to pay.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="404">Unknown membership type.</response>
    /// <response code="409">An unpaid membership already exists (memberships.unpaid_exists).</response>
    /// <response code="422">The type is no longer offered (memberships.type_inactive).</response>
    [HttpPost]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<MembershipResponse>> Buy(
        BuyMembershipRequest request,
        CancellationToken cancellationToken
    )
    {
        var created = await memberships.BuyMineAsync(request, cancellationToken);

        // 201 without a Location header: the single-membership URL (/api/memberships/{id}) is a
        // reception endpoint the member can't open, and pointing them there would be misleading.
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Cancels my unpaid membership.</summary>
    /// <param name="id">Membership id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="404">No such membership of mine.</response>
    /// <response code="422">It is already paid (memberships.already_paid).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await memberships.DeleteMineAsync(id, cancellationToken);
        return NoContent();
    }
}
