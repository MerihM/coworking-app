using Coworking.Application.Auth;

namespace Coworking.Application.Common.Abstractions;

/// <summary>
/// The user who sent the current HTTP request, read from the validated access token.
/// </summary>
/// <remarks>
/// Services use this for ownership checks, e.g. "a member may only cancel their own reservation"
/// (Sprint 5). Role checks normally happen earlier, in policies on the controllers. All values are
/// <c>null</c> for anonymous requests.
/// </remarks>
public interface ICurrentUser
{
    /// <summary><c>true</c> when the request carried a valid access token.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The user's ID inside its own table, or <c>null</c> when anonymous.</summary>
    int? Id { get; }

    /// <summary>Which table <see cref="Id"/> belongs to, or <c>null</c> when anonymous.</summary>
    UserType? UserType { get; }

    /// <summary>One of <see cref="Roles"/>, or <c>null</c> when anonymous.</summary>
    string? Role { get; }
}
