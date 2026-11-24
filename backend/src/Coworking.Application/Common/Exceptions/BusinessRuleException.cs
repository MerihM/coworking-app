namespace Coworking.Application.Common.Exceptions;

/// <summary>
/// The input is well-formed, but a business rule from the design document forbids the operation.
/// Mapped to HTTP 422 (Unprocessable Content).
/// Examples: no active membership (BR02), cancelling less than 2 h before start (BR03),
/// outside working hours (BR04).
/// </summary>
public sealed class BusinessRuleException : AppException
{
    /// <summary>Creates a business rule violation.</summary>
    /// <param name="ruleCode">Rule code from the design document, e.g. "BR04". Always required here.</param>
    /// <param name="message">Explanation for the user.</param>
    public BusinessRuleException(string ruleCode, string message)
        : base(message, ruleCode) { }
}
