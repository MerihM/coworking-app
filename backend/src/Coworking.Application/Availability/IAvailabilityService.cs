using Coworking.Application.Availability.Dtos;

namespace Coworking.Application.Availability;

/// <summary>Free workspaces for a date and time (FR02, UC02). Public: visitors and members.</summary>
public interface IAvailabilityService
{
    /// <summary>Finds the workspaces that are free for the whole requested period.</summary>
    /// <param name="query">Local date, start and end time, optional type and minimum capacity.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Free workspaces grouped by room (rooms without free workspaces are left out).</returns>
    /// <exception cref="FluentValidation.ValidationException">Malformed search (400).</exception>
    /// <exception cref="Coworking.Domain.Common.DomainException">Outside working hours (422, ruleCode BR04).</exception>
    /// <exception cref="Coworking.Application.Common.Exceptions.BusinessRuleException">The period has ended (422, availability.in_past).</exception>
    Task<AvailabilityResponse> SearchAsync(AvailabilityQuery query, CancellationToken cancellationToken = default);
}
