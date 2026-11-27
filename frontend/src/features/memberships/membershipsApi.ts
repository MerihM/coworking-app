// Calls to the member's own memberships (FR03, UC03): /api/memberships/mine.
// Reception's list, payment and delete (/api/memberships) are at the end of the file (CW-28).
// Every function returns an ApiResult instead of throwing.
import { api } from "../../api/client";
import { callApi } from "../../api/result";
import type { components, paths } from "../../api/schema";

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

// ----- Reception (CW-28): /api/memberships, Receptionist or Manager -----

export type MembershipPage = components["schemas"]["PagedResultOfMembershipResponse"];
export type RecordPaymentRequest = components["schemas"]["RecordPaymentRequest"];

/** ?Page=&PageSize=&MemberId=&Status=&Search= – names exactly as the API expects them. */
export type MembershipListQuery = NonNullable<
  paths["/api/memberships"]["get"]["parameters"]["query"]
>;

/** The two payment methods, in the order shown in the payment dialog. */
export const paymentMethods = ["Cash", "Card"] as const satisfies readonly PaymentMethod[];

/** Reception's list: e.g. { Status: "Unpaid" } (oldest first) or { MemberId } (one member's history). */
export const listMemberships = (query: MembershipListQuery) =>
  callApi<MembershipPage>(() => api.GET("/api/memberships", { params: { query } }));

/** Records the payment (amount must equal the price, BR05) and returns the now active membership. */
export const recordPayment = (id: number, body: RecordPaymentRequest) =>
  callApi<Membership>(() =>
    api.POST("/api/memberships/{id}/payment", { params: { path: { id } }, body }),
  );

/** Deletes an UNPAID membership (a paid one answers 422 memberships.already_paid). */
export const deleteMembership = (id: number) =>
  callApi<void>(() => api.DELETE("/api/memberships/{id}", { params: { path: { id } } }));
