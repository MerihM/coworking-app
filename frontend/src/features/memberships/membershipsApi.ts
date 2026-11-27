// Calls to the member's own memberships (FR03, UC03): /api/memberships/mine.
// Reception's endpoints (/api/memberships, /api/members/{id}/memberships) are added in CW-28.
// Every function returns an ApiResult instead of throwing.
import { api } from "../../api/client";
import { callApi } from "../../api/result";
import type { components } from "../../api/schema";

export type Membership = components["schemas"]["MembershipResponse"];
export type MembershipStatus = components["schemas"]["MembershipStatus"];
export type PaymentMethod = components["schemas"]["PaymentMethod"];

/** My memberships, newest purchase first (no paging – a short history). */
export const listMyMemberships = () =>
  callApi<Membership[]>(() => api.GET("/api/memberships/mine"));

/** Buys a membership of an active type. It stays Unpaid until it is paid at the reception (BR05). */
export const buyMembership = (membershipTypeId: number) =>
  callApi<Membership>(() => api.POST("/api/memberships/mine", { body: { membershipTypeId } }));

/** Cancels my own UNPAID membership. */
export const cancelMyMembership = (id: number) =>
  callApi<void>(() => api.DELETE("/api/memberships/mine/{id}", { params: { path: { id } } }));
