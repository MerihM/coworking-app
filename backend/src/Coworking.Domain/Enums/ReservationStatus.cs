namespace Coworking.Domain.Enums;

/// <summary>Status of a reservation (design document: StatusRezervacije).</summary>
public enum ReservationStatus
{
    /// <summary>Active reservation that occupies the workspace (Potvrdjena).</summary>
    Confirmed,

    /// <summary>Cancelled by the member or by an employee; frees the workspace (Otkazana).</summary>
    Cancelled,

    /// <summary>The reserved period has passed (Iskoristena).</summary>
    Completed,
}
