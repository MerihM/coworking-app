namespace Coworking.Application.Rooms.Dtos;

/// <summary>
/// Body of <c>PUT /api/rooms/{id}</c>. PUT = the whole editable state; sending
/// <c>description: null</c> removes the description. The active flag has its own endpoints
/// (deactivate/activate), so an edit can't switch it by accident.
/// </summary>
/// <param name="Name">New name (unique, letter case ignored).</param>
/// <param name="Floor">New floor.</param>
/// <param name="Description">New description, or null for none.</param>
public sealed record UpdateRoomRequest(string Name, int Floor, string? Description);
