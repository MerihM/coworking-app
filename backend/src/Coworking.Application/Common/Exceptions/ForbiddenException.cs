namespace Coworking.Application.Common.Exceptions;

/// <summary>
/// The user is logged in but is not allowed to perform this operation on this resource.
/// Mapped to HTTP 403. Example: a member tries to cancel another member's reservation.
/// </summary>
/// <remarks>
/// Role checks ("only a Manager may open reports", TC08) are handled by ASP.NET Core
/// authorization attributes in Sprint 2; this exception is for checks that need the data itself.
/// </remarks>
public sealed class ForbiddenException : AppException
{
    /// <summary>Creates a forbidden error.</summary>
    /// <param name="message">Explanation for the user.</param>
    public ForbiddenException(string message = "You are not allowed to perform this operation.")
        : base(message) { }
}
