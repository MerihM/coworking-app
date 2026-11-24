namespace Coworking.Domain.Common;

/// <summary>
/// Thrown by entities when an operation would break a business rule or put the entity
/// into an invalid state (e.g. paying a membership twice, cancelling too late).
/// The API maps it to HTTP 422 with the optional rule code (see GlobalExceptionHandler).
/// </summary>
/// <remarks>
/// The Domain project has no references, so it cannot use the Application exceptions;
/// this is its own, equivalent type. Invalid ARGUMENTS (empty name, negative price) are
/// not domain errors: they throw ArgumentException, because input is validated earlier
/// by FluentValidation, so reaching an entity with bad input means a bug (HTTP 500).
/// </remarks>
public sealed class DomainException : Exception
{
    /// <summary>Creates a domain error.</summary>
    /// <param name="message">Explanation, safe to show to the user (no personal data).</param>
    /// <param name="ruleCode">Business rule code from the design document, e.g. "BR03".</param>
    public DomainException(string message, string? ruleCode = null)
        : base(message)
    {
        RuleCode = ruleCode;
    }

    /// <summary>Code of the violated business rule (BR01–BR05), if the error comes from one.</summary>
    public string? RuleCode { get; }
}
