namespace Coworking.Application.Members.Dtos;

/// <summary>
/// Body of <c>POST /api/members</c>: reception registers a member at the desk (FR08, UC10).
/// Same fields as self-registration (FR01) plus an optional fixed desk.
/// </summary>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">Login e-mail; must not be used by any member or employee.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="Password">Initial password (password policy applies); reception tells it to the member.</param>
/// <param name="FixedWorkspaceId">Id of an active FixedDesk to assign, or null.</param>
public sealed record CreateMemberRequest(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Password,
    int? FixedWorkspaceId
);
