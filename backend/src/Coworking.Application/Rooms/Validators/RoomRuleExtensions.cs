using Coworking.Application.Common.Localization;
using FluentValidation;

namespace Coworking.Application.Rooms.Validators;

/// <summary>
/// Field rules shared by create and update. Lengths match RoomConfiguration (name 100,
/// description 1000), so valid input always fits the table.
/// </summary>
internal static class RoomRuleExtensions
{
    /// <summary>Lowest floor we accept (deep basement / garage levels).</summary>
    public const int MinFloor = -5;

    /// <summary>Highest floor we accept. Generous on purpose – it only catches typos like 1000.</summary>
    public const int MaxFloor = 100;

    public static IRuleBuilderOptions<T, string> ValidRoomName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            // Trim first: "  A " is one visible character and would be stored as "A".
            .Must(name => name.Trim().Length >= 2)
            .WithMessage(_ =>
                Localized.Text(
                    "Name must be at least 2 characters long.",
                    "Naziv mora imati najmanje 2 znaka."
                )
            )
            .MaximumLength(100)
            .WithName(_ => Localized.Text("Name", "Naziv"));

    public static IRuleBuilderOptions<T, int> ValidFloor<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(MinFloor, MaxFloor).WithName(_ => Localized.Text("Floor", "Sprat"));

    public static IRuleBuilderOptions<T, string?> ValidDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(1000).WithName(_ => Localized.Text("Description", "Opis"));
}
