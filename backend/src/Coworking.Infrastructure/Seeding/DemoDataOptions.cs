namespace Coworking.Infrastructure.Seeding;

/// <summary>
/// Demo data switches from the configuration section <c>Seed</c> (CW-22):
/// <c>Seed:DemoData</c> (on/off) and <c>Seed:DemoPassword</c> (password of the demo accounts).
/// </summary>
/// <remarks>
/// <c>DemoData</c> is not a secret: it is <c>true</c> in appsettings.Development.json and
/// <c>false</c> everywhere else. <c>DemoPassword</c> IS a secret (even if only a demo one): it lives
/// in dotnet user-secrets, never in a committed file. Without it, rooms and prices are still seeded,
/// only the demo accounts are skipped.
/// </remarks>
public sealed class DemoDataOptions
{
    /// <summary>Name of the configuration section (shared with <see cref="SeedAdminOptions"/>, which is <c>Seed:Admin</c>).</summary>
    public const string SectionName = "Seed";

    /// <summary>Seed the demo center on startup. Off by default, so production never gets demo data by accident.</summary>
    public bool DemoData { get; set; }

    /// <summary>Password of the three demo accounts (password policy applies). Null = don't create accounts.</summary>
    public string? DemoPassword { get; set; }
}
