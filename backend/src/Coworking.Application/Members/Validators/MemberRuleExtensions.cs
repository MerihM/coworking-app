using Coworking.Application.Common.Localization;
using FluentValidation;

namespace Coworking.Application.Members.Validators;

/// <summary>
/// Field rules shared by create and edit. Limits match MemberConfiguration (names 100, e-mail 256,
/// phone 30) and repeat the registration rules (RegisterMemberRequestValidator), so a member made
/// at the desk follows exactly the same rules as one who registered online.
/// </summary>
internal static class MemberRuleExtensions
{
    public static IRuleBuilderOptions<T, string> ValidFirstName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(100).WithName(_ => Localized.Text("First name", "Ime"));

    public static IRuleBuilderOptions<T, string> ValidLastName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(100).WithName(_ => Localized.Text("Last name", "Prezime"));

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(256).EmailAddress().WithName(_ => Localized.Text("E-mail", "E-mail"));

    // Same pattern as registration: digits, spaces and + ( ) / -.
    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MaximumLength(30)
            .Matches(@"^\+?\(?[0-9][0-9 ()/-]{5,29}$")
            .WithMessage(_ =>
                Localized.Text(
                    "Phone number may contain only digits, spaces and + ( ) / -.",
                    "Broj telefona smije sadržavati samo cifre, razmake i + ( ) / -."
                )
            )
            .WithName(_ => Localized.Text("Phone", "Telefon"));

    public static IRuleBuilderOptions<T, int?> ValidWorkspaceId<T>(this IRuleBuilder<T, int?> rule) =>
        rule.GreaterThan(0).WithName(_ => Localized.Text("Fixed desk", "Fiksno mjesto"));
}
