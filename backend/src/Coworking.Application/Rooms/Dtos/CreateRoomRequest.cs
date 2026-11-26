namespace Coworking.Application.Rooms.Dtos;

/// <summary>Body of <c>POST /api/rooms</c>: a new room (FR09, UC11). New rooms are active.</summary>
/// <param name="Name">Name, unique in the center (letter case ignored), 2–100 characters.</param>
/// <param name="Floor">Floor number, −5 to 100 (0 = ground floor).</param>
/// <param name="Description">Optional description, up to 1000 characters.</param>
public sealed record CreateRoomRequest(string Name, int Floor, string? Description);
