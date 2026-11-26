using Coworking.Application.Availability.Dtos;
using Coworking.Application.Common.Localization;
using Coworking.Domain.Common;
using Coworking.Domain.Enums;
using FluentValidation;

namespace Coworking.Application.Availability.Validators;

/// <summary>
/// Shape of the search (400 when broken). Business rules – working hours (BR04) and "not in the
/// past" – are checked afterwards by the service and answer 422, the same way a reservation will.
/// </summary>
public sealed class AvailabilityQueryValidator : AbstractValidator<AvailabilityQuery>
{
    /// <summary>How far ahead one can search. Matches how far ahead the UI date picker goes.</summary>
    public const int MaxDaysAhead = 90;

    /// <summary>Times must be on a quarter hour (09:00, 09:15, ...) – the slots the UI offers.</summary>
    public const int MinuteStep = 15;

    /// <summary>Defines the rules.</summary>
    /// <param name="time">Current time ("today" for the 90-day limit); a fake in tests.</param>
    public AvailabilityQueryValidator(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Date)
            .NotNull()
            .Must(date => date <= CenterTimeZone.ToLocalDate(time.GetUtcNow()).AddDays(MaxDaysAhead))
            .WithMessage(_ =>
                Localized.Text(
                    $"You can search at most {MaxDaysAhead} days ahead.",
                    $"Možete pretraživati najviše {MaxDaysAhead} dana unaprijed."
                )
            )
            .WithName(_ => Localized.Text("Date", "Datum"));

        RuleFor(x => x.From)
            .NotNull()
            .Must(OnQuarterHour)
            .WithMessage(_ => QuarterHourMessage())
            .WithName(_ => Localized.Text("From", "Od"));

        RuleFor(x => x.To)
            .NotNull()
            .Must(OnQuarterHour)
            .WithMessage(_ => QuarterHourMessage())
            .Must((query, to) => query.From is null || to > query.From)
            .WithMessage(_ =>
                Localized.Text("The end time must be after the start time.", "Vrijeme završetka mora biti nakon početka.")
            )
            .WithName(_ => Localized.Text("To", "Do"));

        RuleFor(x => x.Type)
            .IsInEnum()
            .NotEqual(WorkspaceType.FixedDesk)
            .WithMessage(_ =>
                Localized.Text(
                    "Fixed desks are assigned to members and can't be searched.",
                    "Fiksna mjesta se dodjeljuju članovima i ne mogu se pretraživati."
                )
            )
            .WithName(_ => Localized.Text("Type", "Tip"));

        RuleFor(x => x.MinCapacity).InclusiveBetween(1, 50).WithName(_ => Localized.Text("Minimum capacity", "Najmanji kapacitet"));
    }

    private static bool OnQuarterHour(TimeOnly? time) =>
        time is not { } t || (t.Minute % MinuteStep == 0 && t.Second == 0 && t.Millisecond == 0);

    private static string QuarterHourMessage() =>
        Localized.Text(
            "Times must be on a quarter hour (e.g. 09:00, 09:15).",
            "Vrijeme mora biti na četvrt sata (npr. 09:00, 09:15)."
        );
}
