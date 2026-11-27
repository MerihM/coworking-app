using Coworking.Application.Common.Localization;
using Coworking.Application.Memberships.Dtos;
using FluentValidation;

namespace Coworking.Application.Memberships.Validators;

/// <summary>
/// Shape of a payment: a positive amount with at most 2 decimals and a known method. Whether the
/// amount equals the price is a business rule (BR05), checked by the domain → 422.
/// </summary>
public sealed class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
{
    /// <summary>Defines the rules.</summary>
    public RecordPaymentRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Amount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(10_000m)
            .PrecisionScale(10, 2, true)
            .WithName(_ => Localized.Text("Amount", "Iznos"));

        RuleFor(x => x.Method).IsInEnum().WithName(_ => Localized.Text("Payment method", "Način plaćanja"));
    }
}
