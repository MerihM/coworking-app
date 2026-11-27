namespace Coworking.Application.Members.Dtos;

/// <summary>
/// Body of <c>PUT /api/members/{id}</c>. PUT = the whole editable state; the password is not here.
/// </summary>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">Login e-mail.</param>
/// <param name="Phone">Phone number.</param>
/// <param name="FixedWorkspaceId">Fixed desk to assign; null removes the assignment.</param>
public sealed record UpdateMemberRequest(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    int? FixedWorkspaceId
);
