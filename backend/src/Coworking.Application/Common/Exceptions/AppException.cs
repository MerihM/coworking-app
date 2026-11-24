namespace Coworking.Application.Common.Exceptions;

/// <summary>
/// Base class for all EXPECTED application errors (not found, conflict, broken business rule...).
/// Services throw these; the API's GlobalExceptionHandler translates each type into
/// the matching HTTP status code and a ProblemDetails (RFC 7807) response.
/// Anything that is NOT an AppException is treated as an unexpected bug (HTTP 500).
/// </summary>
/// <remarks>
/// Messages are returned to the client and written to the log, so they must
/// never contain passwords, tokens or personal data (NFR01, NFR05).
/// </remarks>
public abstract class AppException : Exception
{
    /// <summary>Creates an application error.</summary>
    /// <param name="message">Human-readable description, safe to show to the user.</param>
    /// <param name="ruleCode">Optional business rule code from the design document (e.g. "BR04").</param>
    protected AppException(string message, string? ruleCode = null)
        : base(message)
    {
        RuleCode = ruleCode;
    }

    /// <summary>
    /// Code of the violated business rule (BR01–BR05), if any.
    /// Returned to the client as the "ruleCode" field so the frontend can react to specific rules.
    /// </summary>
    public string? RuleCode { get; }
}
