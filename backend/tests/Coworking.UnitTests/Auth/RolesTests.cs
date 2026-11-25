using Coworking.Application.Auth;
using Coworking.Domain.Enums;

namespace Coworking.UnitTests.Auth;

/// <summary>
/// The role claim of an employee is written as <c>EmployeeRole.ToString()</c>, and policies compare
/// it with the constants in <see cref="Roles"/>. If someone renames an enum member, these tests
/// fail instead of every staff user silently losing access.
/// </summary>
public sealed class RolesTests
{
    [Fact]
    public void EveryEmployeeRole_HasAMatchingRoleConstant()
    {
        foreach (var name in Enum.GetNames<EmployeeRole>())
        {
            Assert.Contains(name, Roles.All);
        }
    }

    [Fact]
    public void MemberRole_IsNotAnEmployeeRole()
    {
        Assert.DoesNotContain(Roles.Member, Enum.GetNames<EmployeeRole>());
    }

    [Theory]
    [InlineData(UserType.Member, "member")]
    [InlineData(UserType.Employee, "employee")]
    public void UserTypeClaimValues_RoundTrip(UserType userType, string claimValue)
    {
        Assert.Equal(claimValue, UserTypeClaimValues.From(userType));
        Assert.True(UserTypeClaimValues.TryParse(claimValue, out var parsed));
        Assert.Equal(userType, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Member")] // claim values are lowercase on purpose
    [InlineData("admin")]
    public void UserTypeClaimValues_UnknownValue_IsRejected(string? value)
    {
        Assert.False(UserTypeClaimValues.TryParse(value, out _));
    }
}
