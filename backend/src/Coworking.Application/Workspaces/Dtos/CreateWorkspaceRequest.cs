using Coworking.Domain.Enums;

namespace Coworking.Application.Workspaces.Dtos;

/// <summary>Body of <c>POST /api/workspaces</c>: a new, active workspace in an active room (FR09, UC11).</summary>
/// <param name="RoomId">The room it goes into (404 if unknown, 422 if deactivated).</param>
/// <param name="Code">Unique label: letters, digits and "-", max 20 (e.g. "os-01" is stored as "OS-01").</param>
/// <param name="Type">HotDesk, FixedDesk, Office or MeetingRoom.</param>
/// <param name="Capacity">Desks: exactly 1; Office: 1–20; MeetingRoom: 2–50.</param>
public sealed record CreateWorkspaceRequest(int RoomId, string Code, WorkspaceType Type, int Capacity)
    : IWorkspaceInput;
