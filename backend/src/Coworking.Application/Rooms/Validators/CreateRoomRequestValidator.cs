using Coworking.Application.Rooms.Dtos;
using FluentValidation;

namespace Coworking.Application.Rooms.Validators;

/// <summary>Input rules for a new room (FR09).</summary>
public sealed class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequest>
{
    /// <summary>Defines the rules.</summary>
    public CreateRoomRequestValidator()
    {
        // Stop at the first failing rule of a property, so "Name" shows one message, not three.
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Name).ValidRoomName();
        RuleFor(x => x.Floor).ValidFloor();
        RuleFor(x => x.Description).ValidDescription();
    }
}
