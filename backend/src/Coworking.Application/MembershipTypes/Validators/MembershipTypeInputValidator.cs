using Coworking.Application.Common.Localization;
using Coworking.Application.MembershipTypes.Dtos;
using FluentValidation;

namespace Coworking.Application.MembershipTypes.Validators;

/// <summary>
/// Rules shared by create and update. Limits match the table (name varchar(100), price
/// numeric(10,2), CHECKs duration &gt; 0 and price &gt;= 0) but are stricter where it makes sense.
/// </summary>
/// <typeparam name="T">The request type.</typeparam>
public abstract class MembershipTypeInputValidator<T> : AbstractValidator<T>
    where T : IMembershipTypeInput
{
    /// <summary>Longest duration we accept: one year. Longer plans are unusual and probably typos.</summary>
    public const int MaxDurationDays = 365;

    /// <summary>Lowest price. 0 is allowed by the DB, but a free plan isn't part of the business (BR05 needs a payment).</summary>
    public const decimal MinPrice = 0.01m;

    /// <summary>Highest price – only catches typos like 18000 instead of 180.</summary>
    public const decimal MaxPrice = 10_000m;

    /// <summary>Defines the rules.</summary>
    protected MembershipTypeInputValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Name)
            .NotEmpty()
            .Must(name => name.Trim().Length >= 2)
            .WithMessage(_ =>
                Localized.Text("Name must be at least 2 characters long.", "Naziv mora imati najmanje 2 znaka.")
            )
            .MaximumLength(100)
            .WithName(_ => Localized.Text("Name", "Naziv"));

        RuleFor(x => x.DurationDays)
            .InclusiveBetween(1, MaxDurationDays)
            .WithName(_ => Localized.Text("Duration (days)", "Trajanje (dana)"));

        RuleFor(x => x.Price)
            .InclusiveBetween(MinPrice, MaxPrice)
            // numeric(10,2): at most 2 decimals. ignoreTrailingZeros: 15.50 and 15.500 are both fine,
            // 15.505 is not (we never round money silently).
            .PrecisionScale(10, 2, ignoreTrailingZeros: true)
            .WithName(_ => Localized.Text("Price", "Cijena"));
    }
}

/// <summary>Input rules for a new membership type (FR10).</summary>
public sealed class CreateMembershipTypeRequestValidator : MembershipTypeInputValidator<CreateMembershipTypeRequest>;

/// <summary>Input rules for editing a membership type – the same as for creating one.</summary>
public sealed class UpdateMembershipTypeRequestValidator : MembershipTypeInputValidator<UpdateMembershipTypeRequest>;
