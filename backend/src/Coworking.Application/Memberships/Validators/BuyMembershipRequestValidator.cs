using Coworking.Application.Common.Localization;
using Coworking.Application.Memberships.Dtos;
using FluentValidation;

namespace Coworking.Application.Memberships.Validators;

/// <summary>The type id must be present (0 = the field was left out).</summary>
public sealed class BuyMembershipRequestValidator : AbstractValidator<BuyMembershipRequest>
{
    /// <summary>Defines the rules.</summary>
    public BuyMembershipRequestValidator()
    {
        RuleFor(x => x.MembershipTypeId)
            .GreaterThan(0)
            .WithName(_ => Localized.Text("Membership type", "Tip članarine"));
    }
}
