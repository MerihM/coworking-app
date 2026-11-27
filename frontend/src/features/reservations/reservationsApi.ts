// Calls to the reservation endpoints (FR04–FR06, CW-29).
// Member: /api/reservations/mine (+ /api/reservations/alternatives); reception: /api/reservations.
// Every function returns an ApiResult instead of throwing.
import { api } from "../../api/client";
import { callApi } from "../../api/result";
import type { components, paths } from "../../api/schema";

export type Reservation = components["schemas"]["ReservationResponse"];
export type ReservationStatus = components["schemas"]["ReservationStatus"];
export type ReservationScope = components["schemas"]["ReservationScope"];
export type ReservationPage = components["schemas"]["PagedResultOfReservationResponse"];
export type CreateReservationRequest = components["schemas"]["CreateReservationRequest"];
export type ReservationAlternatives = components["schemas"]["ReservationAlternativesResponse"];
export type AlternativeWorkspace = components["schemas"]["AlternativeWorkspace"];
export type AlternativeSlot = components["schemas"]["AlternativeSlot"];

/** ?WorkspaceId=&Date=&From=&To= – names exactly as the API expects them (times "HH:mm"). */
export type AlternativesQuery = NonNullable<
  paths["/api/reservations/alternatives"]["get"]["parameters"]["query"]
>;

/** ?Scope=&Page=&PageSize= */
export type MyReservationsQuery = NonNullable<
  paths["/api/reservations/mine"]["get"]["parameters"]["query"]
>;

/** ?Date=&Status=&Search=&MemberId=&Page=&PageSize= */
export type ReservationListQuery = NonNullable<
  paths["/api/reservations"]["get"]["parameters"]["query"]
>;

/** The error code of BR01 (409) – the UI then offers alternatives (UC06). */
export const SLOT_TAKEN = "reservations.slot_taken";

// ----- Member -----

/** Reserves a workspace (UC04). 409 slot_taken → ask for alternatives; 422 BR02 → offer buying a membership. */
export const createReservation = (body: CreateReservationRequest) =>
  callApi<Reservation>(() => api.POST("/api/reservations/mine", { body }));

/** UC06: other free workspaces at the same time and the next free slots of the same workspace. */
export const getReservationAlternatives = (query: AlternativesQuery) =>
  callApi<ReservationAlternatives>(() =>
    api.GET("/api/reservations/alternatives", { params: { query } }),
  );

/** My reservations: { Scope: "Upcoming" } (soonest first) or { Scope: "Past" } (newest first). */
export const listMyReservations = (query: MyReservationsQuery) =>
  callApi<ReservationPage>(() => api.GET("/api/reservations/mine", { params: { query } }));

/** Cancels my reservation – at least 2 h before the start (422 BR03 otherwise). */
export const cancelMyReservation = (id: number) =>
  callApi<void>(() => api.POST("/api/reservations/mine/{id}/cancel", { params: { path: { id } } }));

// ----- Reception: Receptionist or Manager -----

/** All reservations, soonest first, e.g. { Date: "2026-12-01" } for "who is coming that day". */
export const listReservations = (query: ReservationListQuery) =>
  callApi<ReservationPage>(() => api.GET("/api/reservations", { params: { query } }));

/** Cancels any reservation before it ends (also after the member's 2-hour deadline, BR03). */
export const cancelReservation = (id: number) =>
  callApi<void>(() => api.POST("/api/reservations/{id}/cancel", { params: { path: { id } } }));
