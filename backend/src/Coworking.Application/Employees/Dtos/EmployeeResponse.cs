using Coworking.Domain.Entities;
using Coworking.Domain.Enums;

namespace Coworking.Application.Employees.Dtos;

/// <summary>A staff account as shown in the administrator's list and edit form. Never contains the password hash.</summary>
/// <param name="Id">Employee id.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">Login e-mail (lower case).</param>
/// <param name="Role">Receptionist, Manager or Administrator (sent as a string, e.g. "Manager").</param>
/// <param name="IsActive">Deactivated accounts can't log in.</param>
public sealed record EmployeeResponse(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    EmployeeRole Role,
    bool IsActive
)
{
    /// <summary>Maps an entity loaded into memory.</summary>
    /// <param name="employee">The employee.</param>
    /// <returns>The response.</returns>
    public static EmployeeResponse From(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);
        return new EmployeeResponse(
            employee.Id,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.Role,
            employee.IsActive
        );
    }
}
