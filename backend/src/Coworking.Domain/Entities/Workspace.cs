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
    /// <summary>Maximum length of <see cref="Code"/> (column varchar(20)).</summary>
    public const int CodeMaxLength = 20;

    /// <summary>Used by EF Core when loading from the database.</summary>
    private Workspace() { }

    /// <summary>Created only through <see cref="Room.AddWorkspace"/>, so a workspace always belongs to a room.</summary>
    internal Workspace(Room room, string code, WorkspaceType type, int capacity)
    {
        Room = room;
        Update(code, type, capacity);
        IsActive = true;
    }

    /// <summary>Short unique label, e.g. "OS-01" (oznaka). Stored trimmed and in upper case.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>Kind of workspace (tip).</summary>
    public WorkspaceType Type { get; private set; }

    /// <summary>Number of people (kapacitet). Allowed range depends on <see cref="Type"/>, see <see cref="CapacityRange"/>.</summary>
    public int Capacity { get; private set; }

    /// <summary>
    /// Inactive workspaces can't be reserved (aktivno). A workspace is offered only if this flag
    /// AND its room's flag are both true (CW-18).
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>Foreign key of the room.</summary>
    public int RoomId { get; private set; }

    /// <summary>The room this workspace is in.</summary>
    public Room Room { get; private set; } = null!;

    /// <summary>
    /// How many people each type may fit (CW-19). Desks are for exactly one person; an office for
    /// up to a small team; a meeting room needs at least two people to make sense. The same rule
    /// is a CHECK constraint in the database (ck_workspaces_capacity_by_type) and a validator rule.
    /// </summary>
    /// <param name="type">Workspace type.</param>
    /// <returns>Inclusive minimum and maximum capacity.</returns>
    public static (int Min, int Max) CapacityRange(WorkspaceType type) =>
        type switch
        {
            WorkspaceType.HotDesk or WorkspaceType.FixedDesk => (1, 1),
            WorkspaceType.Office => (1, 20),
            WorkspaceType.MeetingRoom => (2, 50),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown workspace type."),
        };

    /// <summary>The canonical form of a code: trimmed, upper case ("  os-01 " → "OS-01").</summary>
    /// <param name="code">Code as typed by the user.</param>
    /// <returns>The normalized code; services compare codes in this form.</returns>
    public static string NormalizeCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return code.Trim().ToUpperInvariant();
    }

    /// <summary>Changes the workspace's data (FR09).</summary>
    /// <param name="code">New label.</param>
    /// <param name="type">New type.</param>
    /// <param name="capacity">New capacity; must fit <see cref="CapacityRange"/> of the new type.</param>
    /// <exception cref="DomainException">The capacity doesn't fit the type.</exception>
    public void Update(string code, WorkspaceType type, int capacity)
    {
        var (min, max) = CapacityRange(type);
        if (capacity < min || capacity > max)
        {
            // Normally stopped by the validator (400). Reaching this means a caller skipped
            // validation, so it is a 422 with a clear message rather than corrupt data.
            throw new DomainException(
                min == max
                    ? $"A {type} workspace must have a capacity of exactly {min}."
                    : $"A {type} workspace must have a capacity between {min} and {max}."
            );
        }

        Code = NormalizeCode(code);
        Type = type;
        Capacity = capacity;
    }

    /// <summary>Moves the workspace to another room (e.g. a desk carried to another floor).</summary>
    /// <param name="room">Target room; must be active, like for <see cref="Room.AddWorkspace"/>.</param>
    /// <exception cref="DomainException">The target room is deactivated.</exception>
    public void MoveTo(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        if (!room.IsActive)
        {
            throw new DomainException($"Room '{room.Name}' is deactivated; workspaces can't be moved into it.");
        }

        Room = room;
        RoomId = room.Id;
    }

    /// <summary>Takes the workspace out of the offer (FR09). Existing reservations stay.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Puts the workspace back into the offer.</summary>
    public void Reactivate() => IsActive = true;
}
