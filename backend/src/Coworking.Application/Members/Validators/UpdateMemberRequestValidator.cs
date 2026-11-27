using Coworking.Application.Members.Dtos;
using FluentValidation;

namespace Coworking.Application.Members.Validators;

/// <summary>Input rules for editing a member (FR08).</summary>
public sealed class UpdateMemberRequestValidator : AbstractValidator<UpdateMemberRequest>
{
    /// <summary>Defines the rules.</summary>
    public UpdateMemberRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.FirstName).ValidFirstName();
        RuleFor(x => x.LastName).ValidLastName();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Phone).ValidPhone();
        RuleFor(x => x.FixedWorkspaceId).ValidWorkspaceId().When(x => x.FixedWorkspaceId is not null);
    }
}
