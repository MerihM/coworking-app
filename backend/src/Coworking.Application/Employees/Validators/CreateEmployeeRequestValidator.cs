using Coworking.Application.Auth.Validators;
using Coworking.Application.Common.Localization;
using Coworking.Application.Employees.Dtos;
using FluentValidation;

namespace Coworking.Application.Employees.Validators;

/// <summary>Input rules for a new staff account (FR12).</summary>
public sealed class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    /// <summary>Defines the rules.</summary>
    public CreateEmployeeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.FirstName).ValidFirstName();
        RuleFor(x => x.LastName).ValidLastName();
        RuleFor(x => x.Email).ValidEmail();

        // IsInEnum rejects numbers that are not a defined role, e.g. a hand-made request with 99.
        RuleFor(x => x.Role).IsInEnum().WithName(_ => Localized.Text("Role", "Uloga"));

        // Same policy as member registration (8–128, a letter and a digit).
        RuleFor(x => x.Password).ValidPassword().WithName(_ => Localized.Text("Password", "Lozinka"));
    }
}
