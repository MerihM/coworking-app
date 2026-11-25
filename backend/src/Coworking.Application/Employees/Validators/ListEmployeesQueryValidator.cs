using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Employees.Dtos;
using FluentValidation;

namespace Coworking.Application.Employees.Validators;

/// <summary>Paging rules (from the base class) plus the filters of the employee list.</summary>
public sealed class ListEmployeesQueryValidator : PageQueryValidator<ListEmployeesQuery>
{
    /// <summary>Defines the rules.</summary>
    public ListEmployeesQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).WithName(_ => Localized.Text("Search", "Pretraga"));
        RuleFor(x => x.Role).IsInEnum().WithName(_ => Localized.Text("Role", "Uloga"));
    }
}
