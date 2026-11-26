using Coworking.Application.Auth;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Workspaces;
using Coworking.Application.Workspaces.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.Api.Controllers;

/// <summary>
/// Workspace management (FR09, UC11): the reservable desks, offices and meeting rooms inside rooms.
/// Manager only – every action inherits the policy from the class (Administrator 403, anonymous 401).
/// The public view is the availability search (FR02, CW-21).
/// </summary>
/// <param name="workspaces">The workspace use cases.</param>
[ApiController]
[Route("api/workspaces")]
[Authorize(Policy = Policies.ManagerOnly)]
public sealed class WorkspacesController(IWorkspaceService workspaces) : ControllerBase
{
    /// <summary>Lists workspaces, one page at a time, sorted by room (floor, name) and code.</summary>
    /// <param name="query">page, pageSize, roomId, type, isActive, search – all optional.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The requested page.</response>
    /// <response code="400">Invalid paging or filter values.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<WorkspaceResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<WorkspaceResponse>>> List(
        [FromQuery] ListWorkspacesQuery query,
        CancellationToken cancellationToken
    ) => await workspaces.ListAsync(query, cancellationToken);

    /// <summary>Returns one workspace.</summary>
    /// <param name="id">Workspace id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The workspace.</response>
    /// <response code="404">No workspace with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<WorkspaceResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<WorkspaceResponse>> Get(int id, CancellationToken cancellationToken) =>
        await workspaces.GetAsync(id, cancellationToken);

    /// <summary>Creates a new, active workspace in an active room.</summary>
    /// <param name="request">Room id, code, type and capacity.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="201">Created; Location points to the new workspace.</response>
    /// <response code="400">Invalid input (incl. capacity that doesn't fit the type).</response>
    /// <response code="404">The room doesn't exist.</response>
    /// <response code="409">Code already used (code workspaces.code_taken).</response>
    /// <response code="422">The room is deactivated (code workspaces.room_inactive).</response>
    [HttpPost]
    [ProducesResponseType<WorkspaceResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<WorkspaceResponse>> Create(CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var created = await workspaces.CreateAsync(request, cancellationToken);

        // 201 + "Location: /api/workspaces/{id}".
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Changes code, type and capacity; a different roomId moves the workspace.</summary>
    /// <param name="id">Workspace id.</param>
    /// <param name="request">All editable fields.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The updated workspace.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="404">No workspace (or target room) with this id.</response>
    /// <response code="409">Code already used (code workspaces.code_taken).</response>
    /// <response code="422">Target room deactivated, or an assigned fixed desk changes type (codes workspaces.room_inactive, workspaces.fixed_assigned).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<WorkspaceResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<WorkspaceResponse>> Update(
        int id,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken
    ) => await workspaces.UpdateAsync(id, request, cancellationToken);

    /// <summary>Takes the workspace out of the offer.</summary>
    /// <param name="id">Workspace id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deactivated (or already inactive).</response>
    /// <response code="404">No workspace with this id.</response>
    /// <response code="422">Fixed desk assigned to a member (code workspaces.fixed_assigned).</response>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await workspaces.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Puts a deactivated workspace back into the offer (the room's flag is not changed).</summary>
    /// <param name="id">Workspace id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Active.</response>
    /// <response code="404">No workspace with this id.</response>
    [HttpPost("{id:int}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        await workspaces.ActivateAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Deletes a workspace that was never reserved (otherwise: deactivate it).</summary>
    /// <param name="id">Workspace id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="404">No workspace with this id.</response>
    /// <response code="409">The workspace has reservations (code conflict.in_use).</response>
    /// <response code="422">Fixed desk assigned to a member (code workspaces.fixed_assigned).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await workspaces.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
