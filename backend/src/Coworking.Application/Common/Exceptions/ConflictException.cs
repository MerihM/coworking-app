namespace Coworking.Application.Common.Exceptions;

/// <summary>
/// The request conflicts with the current state of the data. Mapped to HTTP 409.
/// Examples: the workspace is already reserved in the requested period (BR01, TC03),
/// or a member with the same e-mail already exists.
/// </summary>
public sealed class ConflictException : AppException
{
    /// <summary>Creates a conflict error.</summary>
    /// <param name="message">Description of the conflict.</param>
    /// <param name="ruleCode">Optional rule code, e.g. "BR01".</param>
    /// <param name="code">Optional error code for the frontend, e.g. "auth.email_taken".</param>
    public ConflictException(string message, string? ruleCode = null, string? code = null)
        : base(message, ruleCode, code) { }
}
