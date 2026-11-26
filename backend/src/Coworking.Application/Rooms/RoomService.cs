using Coworking.Application.Common.Abstractions;
using Coworking.Application.Common.Exceptions;
using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Rooms.Dtos;
using Coworking.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Rooms;

/// <summary>
/// Implements <see cref="IRoomService"/>. Who may call it (Manager only) is decided by the
/// controller's policy; this class validates input, keeps names unique and maps to responses.
/// </summary>
/// <param name="db">The database.</param>
/// <param name="listValidator">Rules for the list query.</param>
/// <param name="createValidator">Rules for a new room.</param>
/// <param name="updateValidator">Rules for an edit.</param>
public sealed class RoomService(
    IAppDbContext db,
    IValidator<ListRoomsQuery> listValidator,
    IValidator<CreateRoomRequest> createValidator,
    IValidator<UpdateRoomRequest> updateValidator
) : IRoomService
{
    /// <inheritdoc />
    public async Task<PagedResult<RoomResponse>> ListAsync(
        ListRoomsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);

        var rooms = db.Rooms.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // ToLower() inside the query becomes SQL lower(...): case-insensitive search.
            var term = query.Search.Trim().ToLowerInvariant();
            rooms = rooms.Where(r => r.Name.ToLower().Contains(term));
        }

        if (query.IsActive is { } isActive)
        {
            rooms = rooms.Where(r => r.IsActive == isActive);
        }

        // Floor, then name: the order a person walking through the building expects.
        // Id last makes the order unique, which paging needs (otherwise rows can jump pages).
        return await ProjectToResponse(rooms.OrderBy(r => r.Floor).ThenBy(r => r.Name).ThenBy(r => r.Id))
            .ToPagedResultAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RoomResponse> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await ProjectToResponse(db.Rooms.AsNoTracking().Where(r => r.Id == id))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Room", id);

    /// <inheritdoc />
    public async Task<RoomResponse> CreateAsync(
        CreateRoomRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureNameIsFreeAsync(request.Name, exceptRoomId: null, cancellationToken);

        var room = new Room(request.Name, request.Floor, request.Description);
        db.Rooms.Add(room);
        await db.SaveChangesAsync(cancellationToken);

        // A brand-new room has no workspaces yet, so the counts are known without a query.
        return new RoomResponse(room.Id, room.Name, room.Floor, room.Description, room.IsActive, 0, 0);
    }

    /// <inheritdoc />
    public async Task<RoomResponse> UpdateAsync(
        int id,
        UpdateRoomRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var room = await FindAsync(id, cancellationToken);
        await EnsureNameIsFreeAsync(request.Name, exceptRoomId: id, cancellationToken);

        room.Update(request.Name, request.Floor, request.Description);
        await db.SaveChangesAsync(cancellationToken);

        // Re-read through the projection to get the workspace counts in the same shape as GET.
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var room = await FindAsync(id, cancellationToken);
        room.Deactivate(); // Idempotent; EF sends no UPDATE when the value didn't change.
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var room = await FindAsync(id, cancellationToken);
        room.Reactivate();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var room = await FindAsync(id, cancellationToken);
        db.Rooms.Remove(room);

        // workspaces.room_id has ON DELETE RESTRICT. If the room still has workspaces, PostgreSQL
        // refuses (SqlState 23001) and GlobalExceptionHandler answers 409 "conflict.in_use" – the
        // same pattern as deleting an employee. The Workspaces collection is NOT loaded here on
        // purpose: EF then just sends DELETE and lets the database decide.
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Entity → response, written as an expression so EF turns it into ONE SQL query: the two
    /// counts become correlated sub-selects, e.g.
    /// <c>(SELECT count(*) FROM workspaces AS w WHERE r.id = w.room_id AND w.is_active)</c>.
    /// </summary>
    private static IQueryable<RoomResponse> ProjectToResponse(IQueryable<Room> rooms) =>
        rooms.Select(r => new RoomResponse(
            r.Id,
            r.Name,
            r.Floor,
            r.Description,
            r.IsActive,
            r.Workspaces.Count,
            r.Workspaces.Count(w => w.IsActive)
        ));

    /// <summary>Loads a tracked room (so changes are saved) or throws 404.</summary>
    private async Task<Room> FindAsync(int id, CancellationToken cancellationToken) =>
        await db.Rooms.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
        ?? throw new NotFoundException("Room", id);

    /// <summary>Names are unique without regard to case; renaming a room to its own name is fine.</summary>
    private async Task EnsureNameIsFreeAsync(string name, int? exceptRoomId, CancellationToken cancellationToken)
    {
        // Compare the same way as the database index ux_rooms_name_lower: lower(trimmed name).
        var normalized = name.Trim().ToLowerInvariant();

        var taken = await db.Rooms.AnyAsync(
            r => r.Name.ToLower() == normalized && r.Id != exceptRoomId,
            cancellationToken
        );

        if (taken)
        {
            throw new ConflictException(
                Localized.Text("A room with this name already exists.", "Prostorija s ovim nazivom već postoji."),
                code: RoomErrorCodes.NameTaken
            );
        }
    }
}
