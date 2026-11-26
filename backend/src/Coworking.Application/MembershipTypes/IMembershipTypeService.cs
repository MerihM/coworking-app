using Coworking.Application.Common.Pagination;
using Coworking.Application.MembershipTypes.Dtos;

namespace Coworking.Application.MembershipTypes;

/// <summary>
/// The membership price list: management for the manager (FR10, UC12: list, view, create, edit,
/// deactivate/activate, delete) and the public offer for visitors (UC03 step 2).
/// </summary>
public interface IMembershipTypeService
{
    /// <summary>One page of membership types (active and inactive), sorted by duration, then name.</summary>
    /// <param name="query">Paging and filters.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task<PagedResult<MembershipTypeResponse>> ListAsync(ListMembershipTypesQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// The active types only, shortest first – the public price list. No paging: a coworking
    /// center offers a handful of plans, not hundreds.
    /// </summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<MembershipTypeOfferItem>> GetOfferAsync(CancellationToken cancellationToken = default);

    /// <summary>One membership type.</summary>
    /// <param name="id">Membership type id.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.NotFoundException">No such type (404).</exception>
    Task<MembershipTypeResponse> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates an active membership type.</summary>
    /// <param name="request">The new type.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.ConflictException">Name already used (409, membership_types.name_taken).</exception>
    Task<MembershipTypeResponse> CreateAsync(CreateMembershipTypeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes name, duration and price. Bought memberships keep their own price and duration.</summary>
    /// <param name="id">Membership type id.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.ConflictException">Name already used (409, membership_types.name_taken).</exception>
    Task<MembershipTypeResponse> UpdateAsync(int id, UpdateMembershipTypeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Stops offering the type. Already inactive = no change. Bought memberships stay valid.</summary>
    /// <param name="id">Membership type id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Offers the type again.</summary>
    /// <param name="id">Membership type id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task ActivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a type nobody has bought. A type with memberships must be deactivated instead
    /// (409 conflict.in_use from the database's foreign key).
    /// </summary>
    /// <param name="id">Membership type id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
