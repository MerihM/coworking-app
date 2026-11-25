namespace Coworking.Application.Auth;

/// <summary>
/// Names of the authorization policies. Controllers use them as
/// <c>[Authorize(Policy = Policies.ManagerOnly)]</c>; the rules behind each name are registered in
/// <c>Coworking.Api.Auth.AuthorizationSetup</c>.
/// </summary>
/// <remarks>
/// NFR01 says every user may use only the functions of their own role, so the policies are strict:
/// an Administrator is NOT automatically a Manager. If a function should be shared, we give it a
/// policy that lists all allowed roles (like <see cref="ReceptionistOrManager"/>).
/// </remarks>
public static class Policies
{
    /// <summary>Only administrators (FR12 – staff accounts).</summary>
    public const string AdministratorOnly = nameof(AdministratorOnly);

    /// <summary>Only managers (FR09, FR10, FR11; TC08).</summary>
    public const string ManagerOnly = nameof(ManagerOnly);

    /// <summary>Receptionists and managers (shared staff functions).</summary>
    public const string ReceptionistOrManager = nameof(ReceptionistOrManager);

    /// <summary>Any employee: receptionist, manager or administrator.</summary>
    public const string StaffOnly = nameof(StaffOnly);

    /// <summary>Only members (buying memberships, own reservations).</summary>
    public const string MemberOnly = nameof(MemberOnly);
}
