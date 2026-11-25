using Coworking.Application.Common.Localization;
using FluentValidation;

namespace Coworking.Application.Employees.Validators;

/// <summary>
/// Field rules shared by create and update. Limits match EmployeeConfiguration
/// (names 100, e-mail 256), so valid input always fits the table.
/// </summary>
internal static class EmployeeRuleExtensions
{
    public static IRuleBuilderOptions<T, string> ValidFirstName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(100).WithName(_ => Localized.Text("First name", "Ime"));

    public static IRuleBuilderOptions<T, string> ValidLastName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(100).WithName(_ => Localized.Text("Last name", "Prezime"));

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(256).EmailAddress().WithName(_ => Localized.Text("E-mail", "E-mail"));
}
