using Coworking.Application.Common.Localization;
using Coworking.Application.Reservations.Dtos;
using FluentValidation;

namespace Coworking.Application.Reservations.Validators;

/// <summary>Same shape rules as a reservation request, with every field required.</summary>
public sealed class ReservationAlternativesQueryValidator : AbstractValidator<ReservationAlternativesQuery>
{
    /// <summary>Defines the rules.</summary>
    /// <param name="time">Current time ("today" for the 90-day limit).</param>
    public ReservationAlternativesQueryValidator(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.WorkspaceId)
            .NotNull()
            .GreaterThan(0)
            .WithName(_ => Localized.Text("Workspace", "Radno mjesto"));

        RuleFor(x => x.Date)
            .NotNull()
            .Must(date => date is { } day && ReservationSlotRules.WithinBookingWindow(day, time))
            .WithMessage(_ => ReservationSlotRules.TooFarAheadMessage())
            .WithName(_ => Localized.Text("Date", "Datum"));

        RuleFor(x => x.From)
            .NotNull()
            .Must(ReservationSlotRules.OnQuarterHour)
            .WithMessage(_ => ReservationSlotRules.QuarterHourMessage())
            .WithName(_ => Localized.Text("From", "Od"));

        RuleFor(x => x.To)
            .NotNull()
            .Must(ReservationSlotRules.OnQuarterHour)
            .WithMessage(_ => ReservationSlotRules.QuarterHourMessage())
            .Must((query, to) => query.From is null || to > query.From)
            .WithMessage(_ => ReservationSlotRules.EndAfterStartMessage())
            .WithName(_ => Localized.Text("To", "Do"));
    }
}
