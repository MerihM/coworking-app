using Coworking.Domain.Enums;

namespace Coworking.Application.Workspaces.Dtos;

/// <summary>
/// Body of <c>PUT /api/workspaces/{id}</c>: the whole editable state. A different
/// <paramref name="RoomId"/> moves the workspace to that room. The active flag has its own endpoints.
/// </summary>
/// <param name="RoomId">Room the workspace should be in (move = another id).</param>
/// <param name="Code">Unique label.</param>
/// <param name="Type">New type (a fixed desk assigned to a member can't change type).</param>
/// <param name="Capacity">Must fit the new type.</param>
public sealed record UpdateWorkspaceRequest(int RoomId, string Code, WorkspaceType Type, int Capacity)
    : IWorkspaceInput;
