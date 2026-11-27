namespace Coworking.Application.Reports.Dtos;

/// <summary>
/// Query string of <c>GET /api/reports/occupancy-revenue</c>, e.g. <c>?from=2026-11-01&amp;to=2026-11-30</c>.
/// Both days are local Sarajevo days and both are included in the report.
/// </summary>
/// <remarks>Nullable so that a missing value is a 400 from the validator, not a silent 0001-01-01.</remarks>
public sealed class ReportPeriodQuery
{
    /// <summary>First day of the report (inclusive).</summary>
    public DateOnly? From { get; set; }

    /// <summary>Last day of the report (inclusive). At most 366 days after <see cref="From"/>, counting both.</summary>
    public DateOnly? To { get; set; }
}
