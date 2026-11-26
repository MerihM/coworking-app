using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;

namespace Coworking.UnitTests.Domain;

/// <summary>Room (Prostorija): data normalization and the active flag (FR09, CW-18).</summary>
public class RoomTests
{
    [Fact]
    public void Constructor_TrimsData_AndRoomIsActive()
    {
        var room = new Room("  Open Space  ", 1, "  Quiet zone near the windows  ");

        Assert.Equal("Open Space", room.Name);
        Assert.Equal(1, room.Floor);
        Assert.Equal("Quiet zone near the windows", room.Description);
        Assert.True(room.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyDescription_IsStoredAsNull(string? description)
    {
        var room = new Room("Office 1", 2, description);

        Assert.Null(room.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyName_Throws(string name)
    {
        // Empty input is stopped by the validator first; reaching the entity with it is a bug.
        Assert.ThrowsAny<ArgumentException>(() => new Room(name, 1, null));
    }

    [Fact]
    public void Update_ChangesAllFields_AndCanRemoveTheDescription()
    {
        var room = new Room("Office 1", 2, "Corner office");

        room.Update(" Office 1A ", -1, null);

        Assert.Equal("Office 1A", room.Name);
        Assert.Equal(-1, room.Floor);
        Assert.Null(room.Description);
    }

    [Fact]
    public void Deactivate_DoesNotTouchTheWorkspacesOwnFlags()
    {
        var room = new Room("Open Space", 1, null);
        var active = room.AddWorkspace("OS-01", WorkspaceType.HotDesk, 1);
        var inactive = room.AddWorkspace("OS-02", WorkspaceType.HotDesk, 1);
        inactive.Deactivate();

        room.Deactivate();

        Assert.False(room.IsActive);
        Assert.True(active.IsActive);
        Assert.False(inactive.IsActive);
    }

    [Fact]
    public void DeactivateThenReactivate_RestoresThePreviousState()
    {
        var room = new Room("Open Space", 1, null);
        var active = room.AddWorkspace("OS-01", WorkspaceType.HotDesk, 1);
        var inactive = room.AddWorkspace("OS-02", WorkspaceType.HotDesk, 1);
        inactive.Deactivate();

        room.Deactivate();
        room.Reactivate();

        Assert.True(room.IsActive);
        Assert.True(active.IsActive);
        Assert.False(inactive.IsActive); // was off before the room was deactivated, stays off
    }

    [Fact]
    public void Deactivate_Twice_IsHarmless()
    {
        var room = new Room("Office 2", 2, null);

        room.Deactivate();
        room.Deactivate();

        Assert.False(room.IsActive);
    }

    [Fact]
    public void AddWorkspace_ToDeactivatedRoom_Throws()
    {
        var room = new Room("Office 3", 2, null);
        room.Deactivate();

        Assert.Throws<DomainException>(() => room.AddWorkspace("OF-3", WorkspaceType.Office, 4));
    }
}
