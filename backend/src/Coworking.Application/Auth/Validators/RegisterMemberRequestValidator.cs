using Coworking.Application.Auth.Dtos;
using Coworking.Application.Common.Localization;
using FluentValidation;

namespace Coworking.Application.Auth.Validators;

/// <summary>
/// Input rules for registration (FR01). Limits match the column sizes in
/// MemberConfiguration (names 100, e-mail 256, phone 30), so valid input always fits the table.
/// Registered automatically by <c>AddValidatorsFromAssembly</c>.
/// </summary>
public sealed class RegisterMemberRequestValidator : AbstractValidator<RegisterMemberRequest>
{
    /// <summary>Defines the rules.</summary>
    public RegisterMemberRequestValidator()
    {
        // Stop at the first failed check per field: an empty password gets "must not be empty",
        // not four messages at once.
        RuleLevelCascadeMode = CascadeMode.Stop;

        // WithName changes the field name inside the message ("'Ime' ne smije biti prazno."),
        // not the key in the "errors" object, which stays "firstName".
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100)
            .WithName(_ => Localized.Text("First name", "Ime"));

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100)
            .WithName(_ => Localized.Text("Last name", "Prezime"));

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress()
            .WithName(_ => Localized.Text("E-mail", "E-mail"));

        // Digits, spaces and + ( ) / - ; e.g. "+387 61 123 456", "061/123-456" or "(033) 123 456".
        // Optional "+", optional "(", then a digit, then 5–29 more allowed characters.
        RuleFor(x => x.Phone)
            .NotEmpty()
            .MaximumLength(30)
            .Matches(@"^\+?\(?[0-9][0-9 ()/-]{5,29}$")
            .WithMessage(_ =>
                Localized.Text(
                    "Phone number may contain only digits, spaces and + ( ) / -.",
                    "Broj telefona smije sadržavati samo cifre, razmake i + ( ) / -."
                )
            )
            .WithName(_ => Localized.Text("Phone", "Telefon"));

        RuleFor(x => x.Password)
            .ValidPassword()
            .WithName(_ => Localized.Text("Password", "Lozinka"));
    }
}
