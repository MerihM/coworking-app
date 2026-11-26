using Coworking.Application.Common.Abstractions;
using Coworking.Application.Common.Exceptions;
using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Workspaces.Dtos;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Workspaces;

/// <summary>
/// Implements <see cref="IWorkspaceService"/>. Who may call it (Manager only) is decided by the
/// controller's policy; this class validates input, keeps codes unique, protects assigned fixed
/// desks and refuses deactivated rooms.
/// </summary>
/// <param name="db">The database.</param>
/// <param name="listValidator">Rules for the list query.</param>
/// <param name="createValidator">Rules for a new workspace.</param>
/// <param name="updateValidator">Rules for an edit.</param>
public sealed class WorkspaceService(
    IAppDbContext db,
    IValidator<ListWorkspacesQuery> listValidator,
    IValidator<CreateWorkspaceRequest> createValidator,
    IValidator<UpdateWorkspaceRequest> updateValidator
) : IWorkspaceService
{
    /// <inheritdoc />
    public async Task<PagedResult<WorkspaceResponse>> ListAsync(
        ListWorkspacesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);

        var workspaces = db.Workspaces.AsNoTracking();

        if (query.RoomId is { } roomId)
        {
            workspaces = workspaces.Where(w => w.RoomId == roomId);
        }

        if (query.Type is { } type)
        {
            workspaces = workspaces.Where(w => w.Type == type);
        }

        if (query.IsActive is { } isActive)
        {
            workspaces = workspaces.Where(w => w.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Codes are stored in upper case, so upper-casing the term is enough – no lower() in SQL.
            var term = query.Search.Trim().ToUpperInvariant();
            workspaces = workspaces.Where(w => w.Code.Contains(term));
        }

        // Grouped the way the building is laid out (floor, room), then by code. Id last = unique order for paging.
        return await Project(
                workspaces
                    .OrderBy(w => w.Room.Floor)
                    .ThenBy(w => w.Room.Name)
                    .ThenBy(w => w.Code)
                    .ThenBy(w => w.Id)
            )
            .ToPagedResultAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkspaceResponse> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await Project(db.Workspaces.AsNoTracking().Where(w => w.Id == id)).SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Workspace", id);

    /// <inheritdoc />
    public async Task<WorkspaceResponse> CreateAsync(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var room = await FindRoomAsync(request.RoomId, cancellationToken);
        EnsureRoomIsActive(room);

        var code = Workspace.NormalizeCode(request.Code);
        await EnsureCodeIsFreeAsync(code, exceptWorkspaceId: null, cancellationToken);

        // The aggregate rule "a workspace always belongs to a room" lives in Room.AddWorkspace.
        // The room is tracked, so EF notices the new item in room.Workspaces and INSERTs it, even
        // though the room's other workspaces were never loaded.
        var workspace = room.AddWorkspace(code, request.Type, request.Capacity);
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(workspace.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkspaceResponse> UpdateAsync(
        int id,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var workspace = await FindAsync(id, cancellationToken);

        if (request.RoomId != workspace.RoomId)
        {
            var room = await FindRoomAsync(request.RoomId, cancellationToken);
            EnsureRoomIsActive(room);
            workspace.MoveTo(room);
        }

        // A member keeps this desk; turning it into a hot desk would leave them assigned to
        // something that can't be assigned. Moving it or renaming it is fine.
        if (workspace.Type == WorkspaceType.FixedDesk && request.Type != WorkspaceType.FixedDesk)
        {
            await EnsureNotAssignedAsync(
                workspace.Id,
                Localized.Text(
                    "This fixed desk is assigned to a member, so its type can't be changed.",
                    "Ovo fiksno mjesto je dodijeljeno članu, pa mu se tip ne može promijeniti."
                ),
                cancellationToken
            );
        }

        var code = Workspace.NormalizeCode(request.Code);
        await EnsureCodeIsFreeAsync(code, exceptWorkspaceId: id, cancellationToken);

        workspace.Update(code, request.Type, request.Capacity);
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var workspace = await FindAsync(id, cancellationToken);
        if (!workspace.IsActive)
        {
            return; // Idempotent: deactivating twice is not an error.
        }

        await EnsureNotAssignedAsync(
            id,
            Localized.Text(
                "This fixed desk is assigned to a member. Remove the assignment before deactivating it.",
                "Ovo fiksno mjesto je dodijeljeno članu. Uklonite dodjelu prije deaktivacije."
            ),
            cancellationToken
        );

        // Future Confirmed reservations stay as they are (Sprint 5 decides what to do with them).
        workspace.Deactivate();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var workspace = await FindAsync(id, cancellationToken);
        workspace.Reactivate();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var workspace = await FindAsync(id, cancellationToken);

        // members.fixed_workspace_id is ON DELETE SET NULL: the database would NOT stop this
        // delete, it would silently take the desk away from the member. So we check ourselves.
        await EnsureNotAssignedAsync(
            id,
            Localized.Text(
                "This fixed desk is assigned to a member. Remove the assignment before deleting it.",
                "Ovo fiksno mjesto je dodijeljeno članu. Uklonite dodjelu prije brisanja."
            ),
            cancellationToken
        );

        db.Workspaces.Remove(workspace);

        // reservations.workspace_id is ON DELETE RESTRICT: a workspace that was ever reserved
        // can't be deleted (history and reports need it). PostgreSQL refuses with 23001 and
        // GlobalExceptionHandler answers 409 "conflict.in_use"; the UI suggests deactivating.
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Entity → response as an expression, so EF builds ONE SQL query: a JOIN to rooms for the
    /// room's name and flag, and an EXISTS sub-query on members for <c>IsAssigned</c>.
    /// </summary>
    private IQueryable<WorkspaceResponse> Project(IQueryable<Workspace> workspaces) =>
        workspaces.Select(w => new WorkspaceResponse(
            w.Id,
            w.Code,
            w.Type,
            w.Capacity,
            w.IsActive,
            w.RoomId,
            w.Room.Name,
            w.Room.IsActive,
            db.Members.Any(m => m.FixedWorkspaceId == w.Id)
        ));

    /// <summary>Loads a tracked workspace (so changes are saved) or throws 404.</summary>
    private async Task<Workspace> FindAsync(int id, CancellationToken cancellationToken) =>
        await db.Workspaces.SingleOrDefaultAsync(w => w.Id == id, cancellationToken)
        ?? throw new NotFoundException("Workspace", id);

    /// <summary>Loads a tracked room or throws 404 (the room id comes from the request body).</summary>
    private async Task<Room> FindRoomAsync(int roomId, CancellationToken cancellationToken) =>
        await db.Rooms.SingleOrDefaultAsync(r => r.Id == roomId, cancellationToken)
        ?? throw new NotFoundException("Room", roomId);

    /// <summary>
    /// The domain also refuses inactive rooms (DomainException → 422 without a code); checking here
    /// first gives the frontend a code it can translate.
    /// </summary>
    private static void EnsureRoomIsActive(Room room)
    {
        if (!room.IsActive)
        {
            throw new BusinessRuleException(
                null,
                Localized.Text(
                    "This room is deactivated. Activate it before adding or moving workspaces into it.",
                    "Ova prostorija je deaktivirana. Aktivirajte je prije dodavanja ili premještanja radnih mjesta u nju."
                ),
                WorkspaceErrorCodes.RoomInactive
            );
        }
    }

    /// <summary>Codes are stored normalized (upper case), so a plain comparison is case-insensitive.</summary>
    private async Task EnsureCodeIsFreeAsync(string normalizedCode, int? exceptWorkspaceId, CancellationToken cancellationToken)
    {
        var taken = await db.Workspaces.AnyAsync(
            w => w.Code == normalizedCode && w.Id != exceptWorkspaceId,
            cancellationToken
        );

        if (taken)
        {
            throw new ConflictException(
                Localized.Text("A workspace with this code already exists.", "Radno mjesto s ovom oznakom već postoji."),
                code: WorkspaceErrorCodes.CodeTaken
            );
        }
    }

    /// <summary>Throws 422 workspaces.fixed_assigned when a member has this desk assigned.</summary>
    private async Task EnsureNotAssignedAsync(int workspaceId, string message, CancellationToken cancellationToken)
    {
        if (await db.Members.AnyAsync(m => m.FixedWorkspaceId == workspaceId, cancellationToken))
        {
            throw new BusinessRuleException(null, message, WorkspaceErrorCodes.FixedAssigned);
        }
    }
}
