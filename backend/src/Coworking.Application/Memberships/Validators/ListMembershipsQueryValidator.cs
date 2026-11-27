using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Memberships.Dtos;
using FluentValidation;

namespace Coworking.Application.Memberships.Validators;

/// <summary>Paging rules (from the base class) plus the filters of the reception's membership list.</summary>
public sealed class ListMembershipsQueryValidator : PageQueryValidator<ListMembershipsQuery>
{
    /// <summary>Defines the rules.</summary>
    public ListMembershipsQueryValidator()
    {
        RuleFor(x => x.MemberId)
            .GreaterThan(0)
            .When(x => x.MemberId is not null)
            .WithName(_ => Localized.Text("Member", "Član"));
        RuleFor(x => x.Status).IsInEnum().WithName(_ => Localized.Text("Status", "Status"));
        RuleFor(x => x.Search).MaximumLength(100).WithName(_ => Localized.Text("Search", "Pretraga"));
    }
}
