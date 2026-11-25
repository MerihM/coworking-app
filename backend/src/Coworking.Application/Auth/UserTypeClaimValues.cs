namespace Coworking.Application.Auth;

/// <summary>
/// Converts <see cref="UserType"/> to and from the string stored in the <c>user_type</c> JWT claim.
/// </summary>
/// <remarks>
/// We write explicit lowercase strings instead of <c>enum.ToString()</c> so that renaming an enum
/// member in C# can never silently change the token format the frontend relies on.
/// </remarks>
public static class UserTypeClaimValues
{
    /// <summary>Claim value for <see cref="UserType.Member"/>.</summary>
    public const string Member = "member";

    /// <summary>Claim value for <see cref="UserType.Employee"/>.</summary>
    public const string Employee = "employee";

    /// <summary>Returns the claim value for a user type.</summary>
    /// <param name="userType">The user type to convert.</param>
    /// <returns><c>"member"</c> or <c>"employee"</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An undefined enum value was passed (a bug).</exception>
    public static string From(UserType userType) =>
        userType switch
        {
            UserType.Member => Member,
            UserType.Employee => Employee,
            _ => throw new ArgumentOutOfRangeException(nameof(userType), userType, "Unknown user type."),
        };

    /// <summary>Parses a claim value back into a <see cref="UserType"/>.</summary>
    /// <param name="value">The raw claim value (may be null when the claim is missing).</param>
    /// <param name="userType">The parsed user type when the method returns <c>true</c>.</param>
    /// <returns><c>true</c> when the value is a known user type.</returns>
    public static bool TryParse(string? value, out UserType userType)
    {
        switch (value)
        {
            case Member:
                userType = UserType.Member;
                return true;
            case Employee:
                userType = UserType.Employee;
                return true;
            default:
                userType = default;
                return false;
        }
    }
}
