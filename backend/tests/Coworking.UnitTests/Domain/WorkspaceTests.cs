using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;

namespace Coworking.UnitTests.Domain;

/// <summary>Workspace (RadnoMjesto): capacity per type, code normalization, moving between rooms (FR09, CW-19).</summary>
public class WorkspaceTests
{
    [Theory]
    [InlineData(WorkspaceType.HotDesk, 1)]
    [InlineData(WorkspaceType.FixedDesk, 1)]
    [InlineData(WorkspaceType.Office, 1)]
    [InlineData(WorkspaceType.Office, 20)]
    [InlineData(WorkspaceType.MeetingRoom, 2)]
    [InlineData(WorkspaceType.MeetingRoom, 50)]
    public void Create_CapacityAtTheLimitsOfItsType_IsAccepted(WorkspaceType type, int capacity)
    {
        var workspace = NewRoom().AddWorkspace("W1", type, capacity);

        Assert.Equal(capacity, workspace.Capacity);
    }

    [Theory]
    [InlineData(WorkspaceType.HotDesk, 2)]
    [InlineData(WorkspaceType.FixedDesk, 2)]
    [InlineData(WorkspaceType.Office, 0)]
    [InlineData(WorkspaceType.Office, 21)]
    [InlineData(WorkspaceType.MeetingRoom, 1)]
    [InlineData(WorkspaceType.MeetingRoom, 51)]
    public void Create_CapacityOutsideItsType_Throws(WorkspaceType type, int capacity)
    {
        Assert.Throws<DomainException>(() => NewRoom().AddWorkspace("W1", type, capacity));
    }

    [Fact]
    public void Create_NormalizesTheCode_AndIsActive()
    {
        var workspace = NewRoom().AddWorkspace("  os-01 ", WorkspaceType.HotDesk, 1);

        Assert.Equal("OS-01", workspace.Code);
        Assert.True(workspace.IsActive);
    }

    [Fact]
    public void Update_ChangingTypeNeedsACapacityThatFitsTheNewType()
    {
        var workspace = NewRoom().AddWorkspace("OF-1", WorkspaceType.Office, 6);

        // An office for 6 can't become a hot desk while keeping capacity 6 ...
        Assert.Throws<DomainException>(() => workspace.Update("OF-1", WorkspaceType.HotDesk, 6));

        // ... but it can with capacity 1. A failed update leaves the old values untouched.
        Assert.Equal(WorkspaceType.Office, workspace.Type);
        workspace.Update("of-1", WorkspaceType.HotDesk, 1);
        Assert.Equal(WorkspaceType.HotDesk, workspace.Type);
        Assert.Equal(1, workspace.Capacity);
    }

    [Fact]
    public void MoveTo_ActiveRoom_ChangesTheRoom()
    {
        var workspace = NewRoom("Open Space").AddWorkspace("OS-01", WorkspaceType.HotDesk, 1);
        var target = NewRoom("Quiet Room");

        workspace.MoveTo(target);

        Assert.Same(target, workspace.Room);
    }

    [Fact]
    public void MoveTo_DeactivatedRoom_Throws()
    {
        var workspace = NewRoom("Open Space").AddWorkspace("OS-01", WorkspaceType.HotDesk, 1);
        var target = NewRoom("Closed Room");
        target.Deactivate();

        Assert.Throws<DomainException>(() => workspace.MoveTo(target));
    }

    [Theory]
    [InlineData(WorkspaceType.HotDesk, 1, 1)]
    [InlineData(WorkspaceType.Office, 1, 20)]
    [InlineData(WorkspaceType.MeetingRoom, 2, 50)]
    public void CapacityRange_MatchesTheRules(WorkspaceType type, int min, int max)
    {
        Assert.Equal((min, max), Workspace.CapacityRange(type));
    }

    private static Room NewRoom(string name = "Open Space") => new(name, 1, null);
}
