namespace Coworking.Application.Employees.Dtos;

/// <summary>Body of <c>POST /api/employees/{id}/reset-password</c>.</summary>
/// <param name="NewPassword">The new password (password policy applies).</param>
public sealed record ResetPasswordRequest(string NewPassword);
