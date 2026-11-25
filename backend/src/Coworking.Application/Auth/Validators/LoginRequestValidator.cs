using Coworking.Application.Auth.Dtos;
using Coworking.Application.Common.Localization;
using FluentValidation;

namespace Coworking.Application.Auth.Validators;

/// <summary>
/// Input rules for login. Deliberately NOT the full password policy: an account created before a
/// policy change must still be able to log in. Only "present" and a length cap (see
/// <see cref="PasswordRules.MaxLength"/> for why the cap matters).
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Defines the rules.</summary>
    public LoginRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .WithName(_ => Localized.Text("E-mail", "E-mail"));

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(PasswordRules.MaxLength)
            .WithName(_ => Localized.Text("Password", "Lozinka"));
    }
}
