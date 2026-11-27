using Coworking.Application.Auth;
using Coworking.Application.Reports;
using Coworking.Application.Reports.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.Api.Controllers;

/// <summary>
/// Manager reports (FR11, UC13 "Generiši izvještaj o zauzetosti i prihodima"). Only the Manager may
/// open them (NFR01, TC08): a Member, a Receptionist and also an Administrator get 403, because roles
/// don't inherit.
/// </summary>
/// <param name="reports">The report use cases.</param>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = Policies.ManagerOnly)]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    /// <summary>Workspace occupancy and revenue for a period of local days (both inclusive, at most 366).</summary>
    /// <param name="query">from and to as yyyy-MM-dd.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The report.</response>
    /// <response code="400">Missing day, the end before the start, or longer than 366 days.</response>
    [HttpGet("occupancy-revenue")]
    [ProducesResponseType<OccupancyRevenueReport>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<OccupancyRevenueReport>> OccupancyRevenue(
        [FromQuery] ReportPeriodQuery query,
        CancellationToken cancellationToken
    ) => await reports.GetOccupancyRevenueAsync(query, cancellationToken);
}
