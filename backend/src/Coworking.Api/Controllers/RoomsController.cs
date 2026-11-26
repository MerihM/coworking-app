using Coworking.Application.Auth;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Rooms;
using Coworking.Application.Rooms.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.Api.Controllers;

/// <summary>
/// Room management (FR09, UC11). Manager only – every action inherits the policy from the class.
/// Roles don't inherit, so the Administrator gets 403 here too; anonymous callers get 401.
/// The public view of rooms is the availability search (FR02, CW-21), not this controller.
/// </summary>
/// <param name="rooms">The room use cases.</param>
[ApiController]
[Route("api/rooms")]
[Authorize(Policy = Policies.ManagerOnly)]
public sealed class RoomsController(IRoomService rooms) : ControllerBase
{
    /// <summary>Lists rooms, one page at a time, sorted by floor and name.</summary>
    /// <param name="query">page, pageSize, search, isActive – all optional.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The requested page.</response>
    /// <response code="400">Invalid paging or filter values.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<RoomResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<RoomResponse>>> List(
        [FromQuery] ListRoomsQuery query,
        CancellationToken cancellationToken
    ) => await rooms.ListAsync(query, cancellationToken);

    /// <summary>Returns one room with its workspace counts.</summary>
    /// <param name="id">Room id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The room.</response>
    /// <response code="404">No room with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<RoomResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RoomResponse>> Get(int id, CancellationToken cancellationToken) =>
        await rooms.GetAsync(id, cancellationToken);

    /// <summary>Creates a new, active room.</summary>
    /// <param name="request">Name, floor and optional description.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="201">Created; Location points to the new room.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="409">Name already used (code rooms.name_taken).</response>
    [HttpPost]
    [ProducesResponseType<RoomResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RoomResponse>> Create(CreateRoomRequest request, CancellationToken cancellationToken)
    {
        var created = await rooms.CreateAsync(request, cancellationToken);

        // 201 + "Location: /api/rooms/{id}".
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Changes name, floor and description of a room.</summary>
    /// <param name="id">Room id.</param>
    /// <param name="request">All editable fields.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The updated room.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="404">No room with this id.</response>
    /// <response code="409">Name already used (code rooms.name_taken).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<RoomResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RoomResponse>> Update(
        int id,
        UpdateRoomRequest request,
        CancellationToken cancellationToken
    ) => await rooms.UpdateAsync(id, request, cancellationToken);

    /// <summary>Hides the room and all its workspaces from the offer.</summary>
    /// <param name="id">Room id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deactivated (or already inactive).</response>
    /// <response code="404">No room with this id.</response>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await rooms.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Puts a deactivated room back into the offer.</summary>
    /// <param name="id">Room id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Active.</response>
    /// <response code="404">No room with this id.</response>
    [HttpPost("{id:int}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        await rooms.ActivateAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Deletes a room that has no workspaces (otherwise: deactivate it).</summary>
    /// <param name="id">Room id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="404">No room with this id.</response>
    /// <response code="409">The room still has workspaces (code conflict.in_use).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await rooms.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
