using Coworking.Application.Auth.Dtos;
using Coworking.Application.Auth.Validators;
using FluentValidation.TestHelper;

namespace Coworking.UnitTests.Auth;

/// <summary>Login only checks presence and length – not the password policy.</summary>
public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void OldPasswordThatBreaksTodaysPolicy_IsStillAccepted()
    {
        // An account created before the policy existed must still be able to log in.
        _validator.TestValidate(new LoginRequest("amra@example.com", "abc")).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyFields_AreRejected()
    {
        var result = _validator.TestValidate(new LoginRequest("", ""));

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void HugePassword_IsRejectedBeforeHashing()
    {
        _validator
            .TestValidate(new LoginRequest("amra@example.com", new string('a', 10_000)))
            .ShouldHaveValidationErrorFor(x => x.Password);
    }
}
