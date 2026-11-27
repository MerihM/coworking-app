using Coworking.Application.Common.Pagination;
using Coworking.Application.Memberships.Dtos;

namespace Coworking.Application.Memberships;

/// <summary>
/// Buying memberships (FR03, UC03) and recording payments (FR07, UC09, BR05). The "mine" methods
/// work on the logged-in member; the others are for reception (access decided by the controllers).
/// </summary>
public interface IMembershipService
{
    /// <summary>The logged-in member's memberships, newest purchase first (no paging: a short history).</summary>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The memberships.</returns>
    Task<IReadOnlyList<MembershipResponse>> ListMineAsync(CancellationToken cancellationToken = default);

    /// <summary>The logged-in member buys a membership; it starts as Unpaid (UC03 step 4).</summary>
    /// <param name="request">The chosen type.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new, unpaid membership with the amount to pay.</returns>
    Task<MembershipResponse> BuyMineAsync(BuyMembershipRequest request, CancellationToken cancellationToken = default);

    /// <summary>The logged-in member cancels their own UNPAID membership (UC03 flow 3b, after the fact).</summary>
    /// <param name="id">Membership id; another member's id gives 404.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task DeleteMineAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Reception's list: filter by member, status or name; paged.</summary>
    /// <param name="query">Paging and filters.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<MembershipResponse>> ListAsync(ListMembershipsQuery query, CancellationToken cancellationToken = default);

    /// <summary>One membership.</summary>
    /// <param name="id">Membership id.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The membership; 404 when unknown.</returns>
    Task<MembershipResponse> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Reception sells a membership to a member at the desk (it starts as Unpaid too).</summary>
    /// <param name="memberId">The member.</param>
    /// <param name="request">The chosen type.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new, unpaid membership.</returns>
    Task<MembershipResponse> SellAsync(int memberId, BuyMembershipRequest request, CancellationToken cancellationToken = default);

    /// <summary>Records the payment by the logged-in employee and activates the membership (BR05).</summary>
    /// <param name="id">Membership id.</param>
    /// <param name="request">Amount (must equal the price) and method.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The now active membership with its period.</returns>
    Task<MembershipResponse> RecordPaymentAsync(int id, RecordPaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reception deletes an UNPAID membership (e.g. the member changed their mind).</summary>
    /// <param name="id">Membership id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
