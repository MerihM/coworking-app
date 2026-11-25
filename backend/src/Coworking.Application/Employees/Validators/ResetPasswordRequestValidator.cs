using Coworking.Application.Auth.Validators;
using Coworking.Application.Common.Localization;
using Coworking.Application.Employees.Dtos;
using FluentValidation;

namespace Coworking.Application.Employees.Validators;

/// <summary>The new password must follow the password policy.</summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    /// <summary>Defines the rules.</summary>
    public ResetPasswordRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.NewPassword)
            .ValidPassword()
            .WithName(_ => Localized.Text("New password", "Nova lozinka"));
    }
}
