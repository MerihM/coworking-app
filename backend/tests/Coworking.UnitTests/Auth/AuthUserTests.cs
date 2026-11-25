using Coworking.Application.Auth;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;

namespace Coworking.UnitTests.Auth;

/// <summary>Building the token identity from Member / Employee entities.</summary>
public sealed class AuthUserTests
{
    [Fact]
    public void FromMember_UsesMemberRoleAndFullName()
    {
        var member = new Member("Amra", "Hodžić", "Amra@Example.com", "+387 61 111 222", "hash", DateTimeOffset.UtcNow);

        var user = AuthUser.FromMember(member);

        Assert.Equal(UserType.Member, user.UserType);
        Assert.Equal(Roles.Member, user.Role);
        Assert.Equal("amra@example.com", user.Email);
        Assert.Equal("Amra Hodžić", user.FullName);
    }

    [Theory]
    [InlineData(EmployeeRole.Receptionist, Roles.Receptionist)]
    [InlineData(EmployeeRole.Manager, Roles.Manager)]
    [InlineData(EmployeeRole.Administrator, Roles.Administrator)]
    public void FromEmployee_UsesEmployeeRole(EmployeeRole role, string expectedRole)
    {
        var employee = new Employee("Emir", "Kovač", "emir@coworking.ba", "hash", role);

        var user = AuthUser.FromEmployee(employee);

        Assert.Equal(UserType.Employee, user.UserType);
        Assert.Equal(expectedRole, user.Role);
        Assert.Equal("Emir Kovač", user.FullName);
    }
}
