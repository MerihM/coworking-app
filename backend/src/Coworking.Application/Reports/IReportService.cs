using Coworking.Application.Reports.Dtos;

namespace Coworking.Application.Reports;

/// <summary>Manager reports (FR11, UC13). Access (ManagerOnly) is decided by the controller.</summary>
public interface IReportService
{
    /// <summary>Occupancy of the workspaces and revenue from payments for a period of local days.</summary>
    /// <param name="query">First and last day (both inclusive).</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The report; 400 when the period is invalid.</returns>
    Task<OccupancyRevenueReport> GetOccupancyRevenueAsync(
        ReportPeriodQuery query,
        CancellationToken cancellationToken = default
    );
}
