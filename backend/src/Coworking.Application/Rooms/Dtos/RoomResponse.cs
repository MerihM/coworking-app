namespace Coworking.Application.Rooms.Dtos;

/// <summary>A room as shown in the manager's list and edit form (FR09).</summary>
/// <param name="Id">Room id.</param>
/// <param name="Name">Name, e.g. "Open Space".</param>
/// <param name="Floor">Floor number (sprat); 0 = ground floor, negative = basement.</param>
/// <param name="Description">Optional description (opis), null when empty.</param>
/// <param name="IsActive">Inactive rooms hide all their workspaces from availability search.</param>
/// <param name="WorkspaceCount">All workspaces in the room, active or not.</param>
/// <param name="ActiveWorkspaceCount">Workspaces whose own flag is active (the room's flag is not considered).</param>
public sealed record RoomResponse(
    int Id,
    string Name,
    int Floor,
    string? Description,
    bool IsActive,
    int WorkspaceCount,
    int ActiveWorkspaceCount
);
