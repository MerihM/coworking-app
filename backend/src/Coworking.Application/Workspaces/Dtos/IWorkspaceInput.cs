using Coworking.Domain.Enums;

namespace Coworking.Application.Workspaces.Dtos;

/// <summary>
/// The fields that create and update share. Exists only so that ONE validator class
/// (<c>WorkspaceInputValidator</c>) can check both requests, including the rule that needs two
/// fields at once (capacity depends on type).
/// </summary>
public interface IWorkspaceInput
{
    /// <summary>Target room.</summary>
    int RoomId { get; }

    /// <summary>Label as typed.</summary>
    string Code { get; }

    /// <summary>Workspace type.</summary>
    WorkspaceType Type { get; }

    /// <summary>Number of people.</summary>
    int Capacity { get; }
}
