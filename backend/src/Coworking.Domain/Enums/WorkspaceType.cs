namespace Coworking.Domain.Enums;

/// <summary>Kind of reservable unit (design document: TipRadnogMjesta).</summary>
public enum WorkspaceType
{
    /// <summary>Flexible desk in the open space, any member can book it (Fleksibilno).</summary>
    HotDesk,

    /// <summary>Desk permanently assigned to one member (Fiksno).</summary>
    FixedDesk,

    /// <summary>Private office (Kancelarija).</summary>
    Office,

    /// <summary>Meeting room (Sala).</summary>
    MeetingRoom,
}
