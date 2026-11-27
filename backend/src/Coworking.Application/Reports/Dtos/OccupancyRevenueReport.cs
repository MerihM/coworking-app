using Coworking.Domain.Enums;

namespace Coworking.Application.Reports.Dtos;

/// <summary>
/// The FR11 / UC13 report: workspace occupancy and revenue for a period. Calculated on request from
/// the reservations and payments and never stored – the design document says "Izvještaj nije klasa".
/// </summary>
/// <param name="From">First local day of the period (inclusive).</param>
/// <param name="To">Last local day of the period (inclusive).</param>
/// <param name="GeneratedAt">When the report was calculated (UTC).</param>
/// <param name="Occupancy">Workspace occupancy.</param>
/// <param name="Revenue">Revenue from membership payments.</param>
public sealed record OccupancyRevenueReport(
    DateOnly From,
    DateOnly To,
    DateTimeOffset GeneratedAt,
    OccupancyReport Occupancy,
    RevenueReport Revenue
);

/// <summary>
/// Occupancy = reserved hours ÷ available hours. Available = opening hours (BR04) × counted
/// workspaces. Counted are reservable workspaces (not fixed desks) that are offered now or had a
/// reservation in the period. Reserved = the part of every non-cancelled reservation inside the period.
/// </summary>
/// <param name="AvailableHours">Opening hours of all counted workspaces together.</param>
/// <param name="ReservedHours">Reserved hours (confirmed and completed reservations).</param>
/// <param name="OccupancyRate">Reserved ÷ available, 0–1 with 4 decimals.</param>
/// <param name="ReservationCount">Non-cancelled reservations in the period.</param>
/// <param name="CancelledCount">Cancelled reservations in the period.</param>
/// <param name="ByType">Per workspace type (hot desk, office, meeting room).</param>
/// <param name="ByRoom">Per room, by floor and name.</param>
/// <param name="Workspaces">Per workspace, highest occupancy first.</param>
public sealed record OccupancyReport(
    decimal AvailableHours,
    decimal ReservedHours,
    decimal OccupancyRate,
    int ReservationCount,
    int CancelledCount,
    IReadOnlyList<OccupancyByType> ByType,
    IReadOnlyList<OccupancyByRoom> ByRoom,
    IReadOnlyList<WorkspaceOccupancy> Workspaces
);

/// <summary>Occupancy of one workspace type.</summary>
/// <param name="Type">The type.</param>
/// <param name="WorkspaceCount">Counted workspaces of this type.</param>
/// <param name="AvailableHours">Their opening hours together.</param>
/// <param name="ReservedHours">Their reserved hours.</param>
/// <param name="OccupancyRate">Reserved ÷ available.</param>
/// <param name="ReservationCount">Non-cancelled reservations.</param>
public sealed record OccupancyByType(
    WorkspaceType Type,
    int WorkspaceCount,
    decimal AvailableHours,
    decimal ReservedHours,
    decimal OccupancyRate,
    int ReservationCount
);

/// <summary>Occupancy of one room.</summary>
/// <param name="RoomId">Room id.</param>
/// <param name="RoomName">Room name.</param>
/// <param name="WorkspaceCount">Counted workspaces in the room.</param>
/// <param name="AvailableHours">Their opening hours together.</param>
/// <param name="ReservedHours">Their reserved hours.</param>
/// <param name="OccupancyRate">Reserved ÷ available.</param>
/// <param name="ReservationCount">Non-cancelled reservations.</param>
public sealed record OccupancyByRoom(
    int RoomId,
    string RoomName,
    int WorkspaceCount,
    decimal AvailableHours,
    decimal ReservedHours,
    decimal OccupancyRate,
    int ReservationCount
);

/// <summary>Occupancy of one workspace.</summary>
/// <param name="WorkspaceId">Workspace id.</param>
/// <param name="Code">Workspace code, e.g. OS-01.</param>
/// <param name="Type">Workspace type.</param>
/// <param name="RoomName">Room it is in.</param>
/// <param name="AvailableHours">Its opening hours in the period.</param>
/// <param name="ReservedHours">Its reserved hours.</param>
/// <param name="OccupancyRate">Reserved ÷ available.</param>
/// <param name="ReservationCount">Non-cancelled reservations.</param>
public sealed record WorkspaceOccupancy(
    int WorkspaceId,
    string Code,
    WorkspaceType Type,
    string RoomName,
    decimal AvailableHours,
    decimal ReservedHours,
    decimal OccupancyRate,
    int ReservationCount
);

/// <summary>Revenue = the recorded membership payments of the period (BR05: a membership counts once it is paid).</summary>
/// <param name="Total">Sum of all payments in KM.</param>
/// <param name="PaymentCount">Number of payments.</param>
/// <param name="ByMembershipType">Per membership type, highest total first.</param>
/// <param name="ByMethod">Per payment method (always both Cash and Card).</param>
/// <param name="ByDay">Per local day, only days with payments, oldest first.</param>
public sealed record RevenueReport(
    decimal Total,
    int PaymentCount,
    IReadOnlyList<RevenueByMembershipType> ByMembershipType,
    IReadOnlyList<RevenueByMethod> ByMethod,
    IReadOnlyList<RevenueByDay> ByDay
);

/// <summary>Revenue of one membership type.</summary>
/// <param name="MembershipTypeId">Membership type id.</param>
/// <param name="Name">Its current name.</param>
/// <param name="PaymentCount">Payments.</param>
/// <param name="Total">Sum in KM.</param>
public sealed record RevenueByMembershipType(int MembershipTypeId, string Name, int PaymentCount, decimal Total);

/// <summary>Revenue of one payment method.</summary>
/// <param name="Method">Cash or Card.</param>
/// <param name="PaymentCount">Payments.</param>
/// <param name="Total">Sum in KM.</param>
public sealed record RevenueByMethod(PaymentMethod Method, int PaymentCount, decimal Total);

/// <summary>Revenue of one local day.</summary>
/// <param name="Date">The local Sarajevo day.</param>
/// <param name="PaymentCount">Payments.</param>
/// <param name="Total">Sum in KM.</param>
public sealed record RevenueByDay(DateOnly Date, int PaymentCount, decimal Total);
