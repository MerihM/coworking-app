namespace Coworking.Application.Employees;

/// <summary>
/// Error codes for staff account management (FR12). Sent as the <c>code</c> field of
/// ProblemDetails; the frontend translates them. Never change an existing value.
/// </summary>
public static class EmployeeErrorCodes
{
    /// <summary>
    /// 422 – an administrator tried to deactivate, delete or change the role of their OWN account.
    /// Prevents locking yourself out by accident; another administrator has to do it.
    /// </summary>
    public const string SelfChange = "employees.self_change";

    /// <summary>
    /// 422 – the change would leave the system without any active administrator, and then
    /// nobody could manage staff accounts any more.
    /// </summary>
    public const string LastAdministrator = "employees.last_admin";
}
