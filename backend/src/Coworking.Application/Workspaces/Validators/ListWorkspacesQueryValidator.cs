using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Workspaces.Dtos;
using FluentValidation;

namespace Coworking.Application.Workspaces.Validators;

/// <summary>Paging rules (from the base class) plus the filters of the workspace list.</summary>
public sealed class ListWorkspacesQueryValidator : PageQueryValidator<ListWorkspacesQuery>
{
    /// <summary>Defines the rules.</summary>
    public ListWorkspacesQueryValidator()
    {
        RuleFor(x => x.RoomId).GreaterThan(0).WithName(_ => Localized.Text("Room", "Prostorija"));
        RuleFor(x => x.Type).IsInEnum().WithName(_ => Localized.Text("Type", "Tip"));
        RuleFor(x => x.Search).MaximumLength(20).WithName(_ => Localized.Text("Search", "Pretraga"));
    }
}
