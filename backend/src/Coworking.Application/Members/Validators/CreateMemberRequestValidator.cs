using Coworking.Application.Auth.Validators;
using Coworking.Application.Common.Localization;
using Coworking.Application.Members.Dtos;
using FluentValidation;

namespace Coworking.Application.Members.Validators;

/// <summary>Input rules for a member created by reception (FR08).</summary>
public sealed class CreateMemberRequestValidator : AbstractValidator<CreateMemberRequest>
{
    /// <summary>Defines the rules.</summary>
    public CreateMemberRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.FirstName).ValidFirstName();
        RuleFor(x => x.LastName).ValidLastName();
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Phone).ValidPhone();
        RuleFor(x => x.Password).ValidPassword().WithName(_ => Localized.Text("Password", "Lozinka"));
        RuleFor(x => x.FixedWorkspaceId).ValidWorkspaceId().When(x => x.FixedWorkspaceId is not null);
    }
}
