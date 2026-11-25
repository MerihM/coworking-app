using Coworking.Domain.Enums;

namespace Coworking.Application.Employees.Dtos;

/// <summary>Body of <c>POST /api/employees</c>: a new staff account (FR12, UC14).</summary>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">Login e-mail; must not be used by any member or employee.</param>
/// <param name="Role">Receptionist, Manager or Administrator.</param>
/// <param name="Password">Initial password (password policy applies). The administrator tells it to the employee.</param>
public sealed record CreateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    EmployeeRole Role,
    string Password
);
