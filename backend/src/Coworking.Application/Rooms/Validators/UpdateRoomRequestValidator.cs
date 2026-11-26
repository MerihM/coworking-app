using Coworking.Application.Rooms.Dtos;
using FluentValidation;

namespace Coworking.Application.Rooms.Validators;

/// <summary>Input rules for editing a room – the same as for creating one.</summary>
public sealed class UpdateRoomRequestValidator : AbstractValidator<UpdateRoomRequest>
{
    /// <summary>Defines the rules.</summary>
    public UpdateRoomRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Name).ValidRoomName();
        RuleFor(x => x.Floor).ValidFloor();
        RuleFor(x => x.Description).ValidDescription();
    }
}
