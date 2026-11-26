using Coworking.Domain.Common;
using Coworking.Domain.Enums;

namespace Coworking.Domain.Entities;

/// <summary>
/// A physical part of the center: open space, private office or meeting room
/// (design document: Prostorija). Groups workspaces (Room 1 – 1..* Workspace). Managed by the manager (FR09).
/// </summary>
public class Room : Entity
{
    private readonly List<Workspace> _workspaces = [];

    /// <summary>Used by EF Core when loading from the database.</summary>
    private Room() { }

    /// <summary>Creates a new, active room.</summary>
    /// <param name="name">Name, e.g. "Open space".</param>
    /// <param name="floor">Floor number (sprat).</param>
    /// <param name="description">Optional description (opis).</param>
    public Room(string name, int floor, string? description)
    {
        Update(name, floor, description);
        IsActive = true;
    }

    /// <summary>Name (naziv).</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Floor (sprat).</summary>
    public int Floor { get; private set; }

    /// <summary>Optional description (opis).</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Inactive rooms are hidden from the offer; FR09 asks for deactivation, not deletion.
    /// Not in the class diagram (only RadnoMjesto has "aktivno") – a documented deviation, see
    /// docs/NAMING.md. A workspace is offered only if BOTH it and its room are active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Workspaces in this room. "At least one" (1..*) can't be enforced while a room is being set up,
    /// so rooms without workspaces are simply not offered for reservations.
    /// </summary>
    public IReadOnlyCollection<Workspace> Workspaces => _workspaces;

    /// <summary>Changes the room's data.</summary>
    /// <param name="name">New name.</param>
    /// <param name="floor">New floor.</param>
    /// <param name="description">New description (null = none).</param>
    public void Update(string name, int floor, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Floor = floor;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    /// <summary>Adds a new workspace to this room (FR09).</summary>
    /// <param name="code">Short label, e.g. "A1" (oznaka).</param>
    /// <param name="type">Kind of workspace.</param>
    /// <param name="capacity">How many people it fits (≥ 1).</param>
    /// <returns>The created workspace.</returns>
    public Workspace AddWorkspace(string code, WorkspaceType type, int capacity)
    {
        if (!IsActive)
        {
            throw new DomainException(
                $"Room '{Name}' is deactivated; workspaces can't be added to it."
            );
        }

        var workspace = new Workspace(this, code, type, capacity);
        _workspaces.Add(workspace);
        return workspace;
    }

    /// <summary>
    /// Takes the whole room out of the offer (FR09). The workspaces' own flags are NOT touched:
    /// availability search checks "workspace active AND room active", so this hides them all.
    /// </summary>
    /// <remarks>
    /// Sprint 3 decision. The first version (CW-6) also deactivated every workspace; then
    /// reactivating the room could not know which workspaces had been inactive BEFORE, so the
    /// manager had to turn them all on again by hand. Keeping the two flags separate means
    /// deactivate → activate brings back exactly the previous state, and the workspaces don't
    /// have to be loaded. Calling it twice is harmless (idempotent).
    /// </remarks>
    public void Deactivate() => IsActive = false;

    /// <summary>Puts the room back into the offer; its workspaces keep their own active/inactive state.</summary>
    public void Reactivate() => IsActive = true;
}
