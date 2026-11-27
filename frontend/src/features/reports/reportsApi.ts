// Calls to the report endpoint (FR11, UC13, CW-32) – Manager only.
// Returns an ApiResult instead of throwing, like every feature wrapper.
import { api } from "../../api/client";
import { callApi } from "../../api/result";
import type { components, paths } from "../../api/schema";

export type OccupancyRevenueReport = components["schemas"]["OccupancyRevenueReport"];
export type OccupancyByType = components["schemas"]["OccupancyByType"];
export type OccupancyByRoom = components["schemas"]["OccupancyByRoom"];
export type WorkspaceOccupancy = components["schemas"]["WorkspaceOccupancy"];
export type RevenueByMembershipType = components["schemas"]["RevenueByMembershipType"];
export type RevenueByMethod = components["schemas"]["RevenueByMethod"];
export type RevenueByDay = components["schemas"]["RevenueByDay"];

/** ?From=&To= – both "YYYY-MM-DD", both inclusive, at most 366 days. */
export type ReportQuery = NonNullable<
  paths["/api/reports/occupancy-revenue"]["get"]["parameters"]["query"]
>;

/** Occupancy of the workspaces and revenue from payments for a period. 400 = invalid period. */
export const getOccupancyRevenueReport = (query: ReportQuery) =>
  callApi<OccupancyRevenueReport>(() =>
    api.GET("/api/reports/occupancy-revenue", { params: { query } }),
  );
