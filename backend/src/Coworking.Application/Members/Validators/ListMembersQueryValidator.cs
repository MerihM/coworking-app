using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Members.Dtos;
using FluentValidation;

namespace Coworking.Application.Members.Validators;

/// <summary>Paging rules (from the base class) plus the filters of the member list.</summary>
public sealed class ListMembersQueryValidator : PageQueryValidator<ListMembersQuery>
{
    /// <summary>Defines the rules.</summary>
    public ListMembersQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).WithName(_ => Localized.Text("Search", "Pretraga"));
    }
}
