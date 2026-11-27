using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Reservations.Dtos;
using FluentValidation;

namespace Coworking.Application.Reservations.Validators;

/// <summary>Paging rules (base class) plus the scope of the member's own list.</summary>
public sealed class MyReservationsQueryValidator : PageQueryValidator<MyReservationsQuery>
{
    /// <summary>Defines the rules.</summary>
    public MyReservationsQueryValidator()
    {
        RuleFor(x => x.Scope).IsInEnum().WithName(_ => Localized.Text("Scope", "Prikaz"));
    }
}
