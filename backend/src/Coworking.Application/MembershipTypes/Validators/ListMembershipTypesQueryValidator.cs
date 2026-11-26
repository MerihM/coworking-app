using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.MembershipTypes.Dtos;
using FluentValidation;

namespace Coworking.Application.MembershipTypes.Validators;

/// <summary>Paging rules (from the base class) plus the filters of the membership type list.</summary>
public sealed class ListMembershipTypesQueryValidator : PageQueryValidator<ListMembershipTypesQuery>
{
    /// <summary>Defines the rules.</summary>
    public ListMembershipTypesQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).WithName(_ => Localized.Text("Search", "Pretraga"));
    }
}
