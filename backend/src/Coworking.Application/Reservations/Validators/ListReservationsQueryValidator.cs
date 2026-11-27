using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Reservations.Dtos;
using FluentValidation;

namespace Coworking.Application.Reservations.Validators;

/// <summary>Paging rules (base class) plus the filters of the reception's reservation list.</summary>
public sealed class ListReservationsQueryValidator : PageQueryValidator<ListReservationsQuery>
{
    /// <summary>Defines the rules.</summary>
    public ListReservationsQueryValidator()
    {
        RuleFor(x => x.MemberId)
            .GreaterThan(0)
            .When(x => x.MemberId is not null)
            .WithName(_ => Localized.Text("Member", "Član"));
        RuleFor(x => x.Status).IsInEnum().WithName(_ => Localized.Text("Status", "Status"));
        RuleFor(x => x.Search).MaximumLength(100).WithName(_ => Localized.Text("Search", "Pretraga"));
    }
}
