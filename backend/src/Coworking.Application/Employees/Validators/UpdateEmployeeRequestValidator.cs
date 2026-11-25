using Coworking.Application.Common.Localization;
using Coworking.Application.Employees.Dtos;
using FluentValidation;

namespace Coworking.Application.Employees.Validators;

/// <summary>Input rules for editing a staff account.</summary>
public sealed class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    /// <summary>Defines the rules.</summary>
    public UpdateEmployeeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.FirstName).ValidFirstName();
        RuleFor(x => x.LastName).ValidLastName();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Role).IsInEnum().WithName(_ => Localized.Text("Role", "Uloga"));
    }
}
