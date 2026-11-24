namespace Coworking.Application.Common.Exceptions;

/// <summary>
/// The requested entity does not exist (or is not visible to the current user). Mapped to HTTP 404.
/// </summary>
public sealed class NotFoundException : AppException
{
    /// <summary>Creates the error with a standard message, e.g. "Workspace with id '7' was not found."</summary>
    /// <param name="resourceName">Entity name in English, e.g. "Workspace".</param>
    /// <param name="key">The identifier that was searched for.</param>
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} with id '{key}' was not found.") { }
}
