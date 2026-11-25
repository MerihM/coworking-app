using Coworking.Domain.Enums;

namespace Coworking.Application.Employees.Dtos;

/// <summary>
/// Body of <c>PUT /api/employees/{id}</c>. PUT = the client sends the whole editable state; every
/// field is required. The password is NOT here – it has its own reset endpoint.
/// </summary>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">Login e-mail.</param>
/// <param name="Role">New role (an administrator can't change their own).</param>
public sealed record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    EmployeeRole Role
);
