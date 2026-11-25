using Coworking.Application.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Coworking.Api.Auth;

/// <summary>
/// Registers the authorization policies named in <see cref="Policies"/>.
/// </summary>
/// <remarks>
/// Authentication answers "who are you?" (the token), authorization answers "are you allowed?".
/// Every policy checks BOTH the role and the user type. The role alone would be enough today, but
/// the user type check makes sure a member token can never pass a staff policy, even if a bug
/// someday writes a wrong role into a token.
/// Controllers that need no specific role are still protected: Program.cs calls
/// <c>MapControllers().RequireAuthorization()</c>, so every controller action requires a logged-in
/// user unless it is marked <c>[AllowAnonymous]</c>.
/// </remarks>
public static class AuthorizationSetup
{
    /// <summary>Adds the policies.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdministratorOnly, policy => policy
                .RequireUserType(UserType.Employee)
                .RequireRole(Roles.Administrator))
            .AddPolicy(Policies.ManagerOnly, policy => policy
                .RequireUserType(UserType.Employee)
                .RequireRole(Roles.Manager))
            .AddPolicy(Policies.ReceptionistOrManager, policy => policy
                .RequireUserType(UserType.Employee)
                .RequireRole(Roles.Receptionist, Roles.Manager))
            .AddPolicy(Policies.StaffOnly, policy => policy
                .RequireUserType(UserType.Employee)
                .RequireRole(Roles.Receptionist, Roles.Manager, Roles.Administrator))
            .AddPolicy(Policies.MemberOnly, policy => policy
                .RequireUserType(UserType.Member)
                .RequireRole(Roles.Member));

        return services;
    }

    /// <summary>Requires the <c>user_type</c> claim to have the given value.</summary>
    /// <remarks>RequireClaim also implies an authenticated user, so anonymous callers get 401, not 403.</remarks>
    private static AuthorizationPolicyBuilder RequireUserType(this AuthorizationPolicyBuilder policy, UserType userType) =>
        policy.RequireClaim(AuthClaimTypes.UserType, UserTypeClaimValues.From(userType));
}
