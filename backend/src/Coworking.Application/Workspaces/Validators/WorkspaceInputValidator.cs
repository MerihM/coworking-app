using System.Text.RegularExpressions;
using Coworking.Application.Common.Localization;
using Coworking.Application.Workspaces.Dtos;
using Coworking.Domain.Entities;
using FluentValidation;

namespace Coworking.Application.Workspaces.Validators;

/// <summary>
/// Rules shared by create and update (any <see cref="IWorkspaceInput"/>). The concrete validators
/// below only exist so DI can find a validator per request type.
/// </summary>
/// <typeparam name="T">The request type.</typeparam>
public abstract class WorkspaceInputValidator<T> : AbstractValidator<T>
    where T : IWorkspaceInput
{
    /// <summary>Defines the rules.</summary>
    protected WorkspaceInputValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.RoomId).GreaterThan(0).WithName(_ => Localized.Text("Room", "Prostorija"));

        RuleFor(x => x.Code)
            .NotEmpty()
            // Checked on the NORMALIZED code (trimmed, upper case), exactly what will be stored.
            .Must(WorkspaceCodeFormat.IsValid)
            .WithMessage(_ =>
                Localized.Text(
                    "Code may contain only letters A–Z, digits and '-', must start with a letter or digit and be at most 20 characters long.",
                    "Oznaka smije sadržavati samo slova A–Z, cifre i '-', mora počinjati slovom ili cifrom i imati najviše 20 znakova."
                )
            )
            .WithName(_ => Localized.Text("Code", "Oznaka"));

        RuleFor(x => x.Type).IsInEnum().WithName(_ => Localized.Text("Type", "Tip"));

        RuleFor(x => x.Capacity)
            .Must(FitsType)
            .WithMessage((request, _) => CapacityMessage(request))
            .WithName(_ => Localized.Text("Capacity", "Kapacitet"))
            // An unknown type (e.g. a hand-made request) already fails the Type rule; without
            // this guard CapacityRange would throw and the client would get 500 instead of 400.
            .When(x => Enum.IsDefined(x.Type));
    }

    private static bool FitsType(T request, int capacity)
    {
        var (min, max) = Workspace.CapacityRange(request.Type);
        return capacity >= min && capacity <= max;
    }

    private static string CapacityMessage(T request)
    {
        var (min, max) = Workspace.CapacityRange(request.Type);
        return min == max
            ? Localized.Text(
                $"Capacity for this type must be exactly {min}.",
                $"Kapacitet za ovaj tip mora biti tačno {min}."
            )
            : Localized.Text(
                $"Capacity for this type must be between {min} and {max}.",
                $"Kapacitet za ovaj tip mora biti između {min} i {max}."
            );
    }
}

/// <summary>Input rules for a new workspace (FR09).</summary>
public sealed class CreateWorkspaceRequestValidator : WorkspaceInputValidator<CreateWorkspaceRequest>;

/// <summary>Input rules for editing a workspace – the same as for creating one.</summary>
public sealed class UpdateWorkspaceRequestValidator : WorkspaceInputValidator<UpdateWorkspaceRequest>;

/// <summary>The allowed shape of a workspace code.</summary>
/// <remarks>
/// Its own non-generic class because the [GeneratedRegex] source generator doesn't support
/// members of generic types (like the validator above).
/// </remarks>
internal static partial class WorkspaceCodeFormat
{
    /// <summary>Checks the NORMALIZED code (trimmed, upper case) – exactly what will be stored.</summary>
    /// <param name="code">Code as typed; must not be empty (the NotEmpty rule runs first).</param>
    public static bool IsValid(string code) => Pattern().IsMatch(Workspace.NormalizeCode(code));

    /// <summary>
    /// Letters A–Z, digits and "-", first character not "-", 1–20 characters (column length).
    /// [GeneratedRegex] creates the matching code at compile time: faster and no runtime parsing.
    /// </summary>
    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{0,19}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
