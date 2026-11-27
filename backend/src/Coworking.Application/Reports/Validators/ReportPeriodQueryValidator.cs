using Coworking.Application.Common.Localization;
using Coworking.Application.Reports.Dtos;
using FluentValidation;

namespace Coworking.Application.Reports.Validators;

/// <summary>
/// Shape of a report period: both days required, the end not before the start, at most one year.
/// Past AND future days are allowed – a future period shows the occupancy that is already booked.
/// </summary>
public sealed class ReportPeriodQueryValidator : AbstractValidator<ReportPeriodQuery>
{
    /// <summary>Longest period in days, both ends counted (a leap year).</summary>
    public const int MaxDays = 366;

    /// <summary>Defines the rules.</summary>
    public ReportPeriodQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.From).NotNull().WithName(_ => Localized.Text("From", "Od"));

        RuleFor(x => x.To)
            .NotNull()
            .Must((query, to) => query.From is null || to >= query.From)
            .WithMessage(_ =>
                Localized.Text(
                    "The last day must not be before the first day.",
                    "Posljednji dan ne smije biti prije prvog dana."
                )
            )
            // DayNumber = days since 0001-01-01, so the difference is the number of days between them.
            .Must((query, to) => query.From is null || to!.Value.DayNumber - query.From.Value.DayNumber < MaxDays)
            .WithMessage(_ =>
                Localized.Text(
                    $"A report can cover at most {MaxDays} days.",
                    $"Izvještaj može obuhvatiti najviše {MaxDays} dana."
                )
            )
            .WithName(_ => Localized.Text("To", "Do"));
    }
}
