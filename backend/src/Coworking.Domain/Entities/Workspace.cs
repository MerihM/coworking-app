using Coworking.Domain.Common;
using Coworking.Domain.Enums;

namespace Coworking.Domain.Entities;

/// <summary>
/// The unit that is reserved: a desk, an office or a meeting room (design document: RadnoMjesto).
/// Exists independently of any time period and can be reserved many times.
/// </summary>
/// <remarks>
/// The diagram operation dostupno(pocetak, kraj) (BR01) needs the workspace's reservations
/// from the database, so it is implemented by the reservation service (Sprint 5) using
/// <see cref="Reservation.OverlapsWith"/>, plus a PostgreSQL EXCLUDE constraint (CW-7).
/// </remarks>
public class Workspace : Entity
{
    /// <summary>Used by EF Core when loading from the database.</summary>
    private Workspace() { }

    /// <summary>Created only through <see cref="Room.AddWorkspace"/>, so a workspace always belongs to a room.</summary>
    internal Workspace(Room room, string code, WorkspaceType type, int capacity)
    {
        Room = room;
        Update(code, type, capacity);
        IsActive = true;
    }

    /// <summary>Short unique label, e.g. "A1" (oznaka). Stored in upper case.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>Kind of workspace (tip).</summary>
    public WorkspaceType Type { get; private set; }

    /// <summary>Number of people (kapacitet).</summary>
    public int Capacity { get; private set; }

    /// <summary>Inactive workspaces can't be reserved (aktivno).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Foreign key of the room.</summary>
    public int RoomId { get; private set; }

    /// <summary>The room this workspace is in.</summary>
    public Room Room { get; private set; } = null!;

    /// <summary>Changes the workspace's data (FR09).</summary>
    /// <param name="code">New label.</param>
    /// <param name="type">New type.</param>
    /// <param name="capacity">New capacity (≥ 1).</param>
    public void Update(string code, WorkspaceType type, int capacity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        Code = code.Trim().ToUpperInvariant();
        Type = type;
        Capacity = capacity;
    }

    /// <summary>Takes the workspace out of the offer (FR09). Existing reservations stay.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Puts the workspace back into the offer.</summary>
    public void Reactivate() => IsActive = true;
}
