using Coworking.Application.Common.Pagination;
using Coworking.Application.Members.Dtos;

namespace Coworking.Application.Members;

/// <summary>
/// Member management by reception (FR08, UC10): list/search, create, edit (incl. the fixed desk),
/// deactivate/activate and delete. Access (Receptionist or Manager) is decided by the controller.
/// </summary>
public interface IMemberService
{
    /// <summary>One page of members, sorted by last name, then first name.</summary>
    /// <param name="query">Paging and filters.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<MemberResponse>> ListAsync(ListMembersQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active fixed desks in active rooms, sorted by code, each with the member who has it
    /// (CW-28: choices for the member form).
    /// </summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The desks (35 at most in the demo center, so no paging).</returns>
    Task<IReadOnlyList<FixedDeskOption>> ListFixedDesksAsync(CancellationToken cancellationToken = default);

    /// <summary>One member.</summary>
    /// <param name="id">Member id.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The member; 404 when unknown.</returns>
    Task<MemberResponse> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Registers a member at the desk.</summary>
    /// <param name="request">Personal data, initial password, optional fixed desk.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new member.</returns>
    Task<MemberResponse> CreateAsync(CreateMemberRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes personal data and the fixed desk.</summary>
    /// <param name="id">Member id.</param>
    /// <param name="request">The whole editable state.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated member.</returns>
    Task<MemberResponse> UpdateAsync(int id, UpdateMemberRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deactivates the member and ends their sessions. Idempotent.</summary>
    /// <param name="id">Member id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Activates the member again. Idempotent.</summary>
    /// <param name="id">Member id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task ActivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a member who has no memberships and no reservations (otherwise 409 conflict.in_use).</summary>
    /// <param name="id">Member id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
