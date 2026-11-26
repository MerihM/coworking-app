// Call to GET /api/availability (FR02). Public – works without a login (rate limited on the API).
import { api } from "../../api/client";
import { callApi } from "../../api/result";
import type { components, paths } from "../../api/schema";

export type Availability = components["schemas"]["AvailabilityResponse"];
export type AvailableRoom = components["schemas"]["AvailableRoom"];
export type AvailableWorkspace = components["schemas"]["AvailableWorkspace"];

/** ?Date=&From=&To=&Type=&MinCapacity= – names exactly as the API expects them. */
export type AvailabilityApiQuery = NonNullable<
  paths["/api/availability"]["get"]["parameters"]["query"]
>;

/** Free workspaces for a local date and time, grouped by room. */
export const searchAvailability = (query: AvailabilityApiQuery) =>
  callApi<Availability>(() => api.GET("/api/availability", { params: { query } }));
