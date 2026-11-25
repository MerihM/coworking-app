using Coworking.Application.Common.Pagination;
using Coworking.Domain.Enums;

namespace Coworking.Application.Employees.Dtos;

/// <summary>
/// Query string of <c>GET /api/employees</c>, e.g.
/// <c>?page=1&amp;pageSize=20&amp;search=kovac&amp;role=Manager&amp;isActive=true</c>.
/// Every filter is optional; leaving it out means "don't filter by this".
/// </summary>
public sealed class ListEmployeesQuery : PageQuery
{
    /// <summary>Part of the first name, last name, full name or e-mail (case-insensitive).</summary>
    public string? Search { get; set; }

    /// <summary>Only employees with this role.</summary>
    public EmployeeRole? Role { get; set; }

    /// <summary>true = only active, false = only deactivated.</summary>
    public bool? IsActive { get; set; }
}
