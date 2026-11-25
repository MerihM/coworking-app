using Coworking.Application.Employees.Dtos;
using Coworking.Application.Employees.Validators;
using Coworking.Domain.Enums;
using FluentValidation.TestHelper;

namespace Coworking.UnitTests.Employees;

/// <summary>Input rules for staff account management (FR12).</summary>
public sealed class EmployeeValidatorsTests
{
    private static readonly CreateEmployeeRequest ValidCreate =
        new("Emir", "Kovač", "emir@coworking.ba", EmployeeRole.Receptionist, "Coworking123");

    [Fact]
    public void Create_ValidRequest_HasNoErrors()
    {
        new CreateEmployeeRequestValidator().TestValidate(ValidCreate).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Create_UndefinedRole_IsRejected()
    {
        // A hand-made request could send a number that is not a role.
        new CreateEmployeeRequestValidator()
            .TestValidate(ValidCreate with { Role = (EmployeeRole)99 })
            .ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("abcdefgh")]
    public void Create_WeakPassword_IsRejected(string password)
    {
        new CreateEmployeeRequestValidator()
            .TestValidate(ValidCreate with { Password = password })
            .ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Update_InvalidEmail_IsRejected()
    {
        new UpdateEmployeeRequestValidator()
            .TestValidate(new UpdateEmployeeRequest("Emir", "Kovač", "not-an-email", EmployeeRole.Manager))
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void ResetPassword_FollowsThePolicy()
    {
        var validator = new ResetPasswordRequestValidator();

        validator.TestValidate(new ResetPasswordRequest("abc")).ShouldHaveValidationErrorFor(x => x.NewPassword);
        validator.TestValidate(new ResetPasswordRequest("NewPassword1")).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(100_001, 20)]
    public void List_InvalidPaging_IsRejected(int page, int pageSize)
    {
        var result = new ListEmployeesQueryValidator().TestValidate(
            new ListEmployeesQuery { Page = page, PageSize = pageSize }
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public void List_Defaults_AreValid()
    {
        new ListEmployeesQueryValidator().TestValidate(new ListEmployeesQuery()).ShouldNotHaveAnyValidationErrors();
    }
}
