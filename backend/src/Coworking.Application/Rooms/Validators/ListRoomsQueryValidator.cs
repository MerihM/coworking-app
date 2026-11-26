using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Rooms.Dtos;
using FluentValidation;

namespace Coworking.Application.Rooms.Validators;

/// <summary>Paging rules (from the base class) plus the filters of the room list.</summary>
public sealed class ListRoomsQueryValidator : PageQueryValidator<ListRoomsQuery>
{
    /// <summary>Defines the rules.</summary>
    public ListRoomsQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).WithName(_ => Localized.Text("Search", "Pretraga"));
    }
}
