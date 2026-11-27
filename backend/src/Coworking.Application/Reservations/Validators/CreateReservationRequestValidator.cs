using Coworking.Application.Common.Localization;
using Coworking.Application.Reservations.Dtos;
using FluentValidation;

namespace Coworking.Application.Reservations.Validators;

/// <summary>
/// Shape of a reservation request (400 when broken). The business rules come afterwards in the
/// order of the Activity diagram and answer 422/409: past, BR04, BR02, BR01.
/// </summary>
public sealed class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
{
    /// <summary>Defines the rules.</summary>
    /// <param name="time">Current time ("today" for the 90-day limit); a fake in tests.</param>
    public CreateReservationRequestValidator(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        RuleLevelCascadeMode = CascadeMode.Stop;

        // 0 = the field was left out of the JSON.
        RuleFor(x => x.WorkspaceId)
            .GreaterThan(0)
            .WithName(_ => Localized.Text("Workspace", "Radno mjesto"));

        // NotEmpty refuses default(DateOnly) = 0001-01-01, i.e. a missing "date".
        RuleFor(x => x.Date)
            .NotEmpty()
            .Must(date => ReservationSlotRules.WithinBookingWindow(date, time))
            .WithMessage(_ => ReservationSlotRules.TooFarAheadMessage())
            .WithName(_ => Localized.Text("Date", "Datum"));

        RuleFor(x => x.From)
            .Must(from => ReservationSlotRules.OnQuarterHour(from))
            .WithMessage(_ => ReservationSlotRules.QuarterHourMessage())
            .WithName(_ => Localized.Text("From", "Od"));

        RuleFor(x => x.To)
            .Must(to => ReservationSlotRules.OnQuarterHour(to))
            .WithMessage(_ => ReservationSlotRules.QuarterHourMessage())
            .Must((request, to) => to > request.From)
            .WithMessage(_ => ReservationSlotRules.EndAfterStartMessage())
            .WithName(_ => Localized.Text("To", "Do"));
    }
}
