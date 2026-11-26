// Calls to /api/membership-types (FR10). Management is manager only; the offer is public.
// Every function returns an ApiResult instead of throwing.
import { api } from "../../api/client";
import { callApi } from "../../api/result";
import type { components, paths } from "../../api/schema";

export type MembershipType = components["schemas"]["MembershipTypeResponse"];
export type MembershipTypePage = components["schemas"]["PagedResultOfMembershipTypeResponse"];
export type MembershipTypeOfferItem = components["schemas"]["MembershipTypeOfferItem"];
export type CreateMembershipTypeRequest = components["schemas"]["CreateMembershipTypeRequest"];
export type UpdateMembershipTypeRequest = components["schemas"]["UpdateMembershipTypeRequest"];

/** ?Page=&PageSize=&Search=&IsActive= – names exactly as the API expects them. */
export type MembershipTypeListQuery = NonNullable<
  paths["/api/membership-types"]["get"]["parameters"]["query"]
>;

export const listMembershipTypes = (query: MembershipTypeListQuery) =>
  callApi<MembershipTypePage>(() => api.GET("/api/membership-types", { params: { query } }));

/** The public price list (active types, shortest first). Works without a login – CW-25 Home page. */
export const getMembershipOffer = () =>
  callApi<MembershipTypeOfferItem[]>(() => api.GET("/api/membership-types/offer"));

export const createMembershipType = (body: CreateMembershipTypeRequest) =>
  callApi<MembershipType>(() => api.POST("/api/membership-types", { body }));

export const updateMembershipType = (id: number, body: UpdateMembershipTypeRequest) =>
  callApi<MembershipType>(() =>
    api.PUT("/api/membership-types/{id}", { params: { path: { id } }, body }),
  );

export const deactivateMembershipType = (id: number) =>
  callApi<void>(() =>
    api.POST("/api/membership-types/{id}/deactivate", { params: { path: { id } } }),
  );

export const activateMembershipType = (id: number) =>
  callApi<void>(() =>
    api.POST("/api/membership-types/{id}/activate", { params: { path: { id } } }),
  );

export const deleteMembershipType = (id: number) =>
  callApi<void>(() => api.DELETE("/api/membership-types/{id}", { params: { path: { id } } }));
