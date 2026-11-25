using Coworking.Application.Common.Localization;
using FluentValidation;

namespace Coworking.Application.Auth.Validators;

/// <summary>
/// The password policy (decided at sprint start): 8–128 characters, at least one letter and one
/// digit. Shared by registration (CW-11) and staff accounts / password reset (CW-12). The
/// frontend's zod schema repeats the same rules for instant feedback; this is the one that counts.
/// </summary>
public static class PasswordRules
{
    /// <summary>Minimum length. Length matters more than complexity for real security.</summary>
    public const int MinLength = 8;

    /// <summary>
    /// Maximum length. PBKDF2 has to process the whole password, so without a limit someone could
    /// send a 10 MB "password" and keep the CPU busy (a cheap denial-of-service).
    /// </summary>
    public const int MaxLength = 128;

    /// <summary>Applies the password policy to a string property.</summary>
    /// <typeparam name="T">The validated request type.</typeparam>
    /// <param name="rule">The rule builder from <c>RuleFor(x =&gt; x.Password)</c>.</param>
    /// <returns>The rule builder, for chaining.</returns>
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Length(MinLength, MaxLength)
            // \p{L} = any Unicode letter, so "šifra2024" counts (č, ć, š, ž, đ are letters too).
            .Matches(@"\p{L}")
            .WithMessage(_ =>
                Localized.Text(
                    "Password must contain at least one letter.",
                    "Lozinka mora sadržavati najmanje jedno slovo."
                )
            )
            .Matches("[0-9]")
            .WithMessage(_ =>
                Localized.Text(
                    "Password must contain at least one digit.",
                    "Lozinka mora sadržavati najmanje jednu cifru."
                )
            );
}
