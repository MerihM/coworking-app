namespace Coworking.Infrastructure.Seeding;

/// <summary>
/// The first administrator account, from configuration section <c>Seed:Admin</c>.
/// Never put these values in appsettings.json: in development use dotnet user-secrets, in Docker
/// (Sprint 6) environment variables such as <c>Seed__Admin__Password</c>.
/// </summary>
public sealed class SeedAdminOptions
{
    /// <summary>Name of the configuration section ("Seed:Admin" = nested section Admin inside Seed).</summary>
    public const string SectionName = "Seed:Admin";

    /// <summary>Login e-mail of the first administrator.</summary>
    public string? Email { get; set; }

    /// <summary>Initial password (password policy: 8+ characters, a letter and a digit).</summary>
    public string? Password { get; set; }

    /// <summary>First name shown in the UI.</summary>
    public string FirstName { get; set; } = "System";

    /// <summary>Last name shown in the UI.</summary>
    public string LastName { get; set; } = "Administrator";
}
