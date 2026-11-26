using Coworking.Application.Common.Pagination;
using Coworking.Application.Workspaces.Dtos;

namespace Coworking.Application.Workspaces;

/// <summary>
/// Workspace management for the manager (FR09, UC11): list, view, create, edit (incl. moving to
/// another room), deactivate/activate and delete.
/// </summary>
public interface IWorkspaceService
{
    /// <summary>One page of workspaces, sorted by room (floor, name) and then code.</summary>
    /// <param name="query">Paging and filters.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task<PagedResult<WorkspaceResponse>> ListAsync(ListWorkspacesQuery query, CancellationToken cancellationToken = default);

    /// <summary>One workspace.</summary>
    /// <param name="id">Workspace id.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.NotFoundException">No such workspace (404).</exception>
    Task<WorkspaceResponse> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates an active workspace in an active room.</summary>
    /// <param name="request">The new workspace.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.NotFoundException">Unknown room (404).</exception>
    /// <exception cref="Coworking.Application.Common.Exceptions.ConflictException">Code already used (409, workspaces.code_taken).</exception>
    /// <exception cref="Coworking.Application.Common.Exceptions.BusinessRuleException">Room deactivated (422, workspaces.room_inactive).</exception>
    Task<WorkspaceResponse> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes code, type and capacity, and moves the workspace when the room id differs.</summary>
    /// <param name="id">Workspace id.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.BusinessRuleException">
    /// Target room deactivated (workspaces.room_inactive) or an assigned fixed desk changes type (workspaces.fixed_assigned), 422.
    /// </exception>
    Task<WorkspaceResponse> UpdateAsync(int id, UpdateWorkspaceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Takes the workspace out of the offer. Already inactive = no change.</summary>
    /// <param name="id">Workspace id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.BusinessRuleException">Assigned fixed desk (422, workspaces.fixed_assigned).</exception>
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Puts the workspace back into the offer (its room's flag is not changed).</summary>
    /// <param name="id">Workspace id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task ActivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a workspace that has never been reserved. One with reservations must be
    /// deactivated instead (409 conflict.in_use from the database's foreign key).
    /// </summary>
    /// <param name="id">Workspace id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.BusinessRuleException">Assigned fixed desk (422, workspaces.fixed_assigned).</exception>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
