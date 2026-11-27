using Coworking.Application.Common.Pagination;
using Coworking.Application.Reservations.Dtos;

namespace Coworking.Application.Reservations;

/// <summary>
/// Reservations (FR04–FR06, UC04–UC08). The "mine" methods work on the logged-in member; the others
/// are for reception (access decided by the controllers).
/// </summary>
public interface IReservationService
{
    /// <summary>
    /// The logged-in member reserves a workspace (UC04). Checks in the order of the Activity diagram:
    /// past → BR04 working hours → BR02 active membership on that date → BR01 workspace free.
    /// </summary>
    /// <param name="request">Workspace, local date and times.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The confirmed reservation (the confirmation shown to the member).</returns>
    Task<ReservationResponse> CreateMineAsync(CreateReservationRequest request, CancellationToken cancellationToken = default);

    /// <summary>UC06: other free workspaces at the same time, and the next free slots of the same workspace.</summary>
    /// <param name="query">The slot that was taken.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The offers (either list may be empty).</returns>
    Task<ReservationAlternativesResponse> GetAlternativesAsync(
        ReservationAlternativesQuery query,
        CancellationToken cancellationToken = default
    );

    /// <summary>The logged-in member's reservations (FR06, UC08): upcoming or past, paged.</summary>
    /// <param name="query">Scope and paging.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<ReservationResponse>> ListMineAsync(MyReservationsQuery query, CancellationToken cancellationToken = default);

    /// <summary>The logged-in member cancels their own reservation (FR05, UC07) – at least 2 h before the start (BR03).</summary>
    /// <param name="id">Reservation id; another member's id gives 404.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task CancelMineAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Reception's list of all reservations: filter by date, status, member or text; paged.</summary>
    /// <param name="query">Paging and filters.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The page, soonest start first.</returns>
    Task<PagedResult<ReservationResponse>> ListAsync(ListReservationsQuery query, CancellationToken cancellationToken = default);

    /// <summary>One reservation.</summary>
    /// <param name="id">Reservation id.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The reservation; 404 when unknown.</returns>
    Task<ReservationResponse> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The logged-in employee cancels a reservation on behalf of the member – any time before it ends (BR03).</summary>
    /// <param name="id">Reservation id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task CancelAsync(int id, CancellationToken cancellationToken = default);
}
