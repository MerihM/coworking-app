using Coworking.Application.Common.Pagination;
using Coworking.Application.Rooms.Dtos;

namespace Coworking.Application.Rooms;

/// <summary>
/// Room management for the manager (FR09, UC11): list, view, create, edit, deactivate/activate and
/// delete (instrukcije.txt: input form, list, edit form, delete).
/// </summary>
public interface IRoomService
{
    /// <summary>One page of rooms, filtered, sorted by floor and then name.</summary>
    /// <param name="query">Paging and filters.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task<PagedResult<RoomResponse>> ListAsync(ListRoomsQuery query, CancellationToken cancellationToken = default);

    /// <summary>One room with its workspace counts.</summary>
    /// <param name="id">Room id.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.NotFoundException">No such room (404).</exception>
    Task<RoomResponse> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates an active room without workspaces (they are added in CW-19).</summary>
    /// <param name="request">The new room.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.ConflictException">Name already used (409, rooms.name_taken).</exception>
    Task<RoomResponse> CreateAsync(CreateRoomRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes name, floor and description.</summary>
    /// <param name="id">Room id.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.ConflictException">Name already used (409, rooms.name_taken).</exception>
    Task<RoomResponse> UpdateAsync(int id, UpdateRoomRequest request, CancellationToken cancellationToken = default);

    /// <summary>Hides the room (and therefore all its workspaces) from the offer. Already inactive = no change.</summary>
    /// <param name="id">Room id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Puts the room back into the offer. Already active = no change.</summary>
    /// <param name="id">Room id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task ActivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a room that has no workspaces. A room with workspaces must be emptied or
    /// deactivated instead (409 conflict.in_use from the database's foreign key).
    /// </summary>
    /// <param name="id">Room id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
