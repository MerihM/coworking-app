namespace Coworking.Application.Common.Exceptions;

/// <summary>
/// The input is well-formed, but a rule forbids the operation. Mapped to HTTP 422 (Unprocessable Content).
/// Examples: no active membership (BR02), cancelling less than 2 h before start (BR03),
/// outside working hours (BR04), an administrator deactivating their own account (CW-12).
/// </summary>
public sealed class BusinessRuleException : AppException
{
    /// <summary>Creates a rule violation.</summary>
    /// <param name="ruleCode">
    /// Rule code from the design document, e.g. "BR04". Null for rules that are not in the document
    /// (e.g. "the last administrator can't be deactivated") – those use <paramref name="code"/> only.
    /// </param>
    /// <param name="message">Explanation for the user (English fallback).</param>
    /// <param name="code">Optional error code for the frontend, e.g. "employees.last_admin".</param>
    public BusinessRuleException(string? ruleCode, string message, string? code = null)
        : base(message, ruleCode, code) { }
}
