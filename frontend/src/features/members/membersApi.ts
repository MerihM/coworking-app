// Calls to /api/members (FR08, UC10) – reception's member management, Receptionist or Manager.
// Every function returns an ApiResult instead of throwing.
import { api } from "../../api/client";
import { callApi } from "../../api/result";
import type { components, paths } from "../../api/schema";
import type { Membership } from "../memberships/membershipsApi";

export type Member = components["schemas"]["MemberResponse"];
export type MemberPage = components["schemas"]["PagedResultOfMemberResponse"];
export type CreateMemberRequest = components["schemas"]["CreateMemberRequest"];
export type UpdateMemberRequest = components["schemas"]["UpdateMemberRequest"];
export type FixedDeskOption = components["schemas"]["FixedDeskOption"];

/** ?Page=&PageSize=&Search=&IsActive= – names exactly as the API expects them. */
export type MemberListQuery = NonNullable<paths["/api/members"]["get"]["parameters"]["query"]>;

export const listMembers = (query: MemberListQuery) =>
  callApi<MemberPage>(() => api.GET("/api/members", { params: { query } }));

export const getMember = (id: number) =>
  callApi<Member>(() => api.GET("/api/members/{id}", { params: { path: { id } } }));

export const createMember = (body: CreateMemberRequest) =>
  callApi<Member>(() => api.POST("/api/members", { body }));

export const updateMember = (id: number, body: UpdateMemberRequest) =>
  callApi<Member>(() => api.PUT("/api/members/{id}", { params: { path: { id } }, body }));

export const deactivateMember = (id: number) =>
  callApi<void>(() => api.POST("/api/members/{id}/deactivate", { params: { path: { id } } }));

export const activateMember = (id: number) =>
  callApi<void>(() => api.POST("/api/members/{id}/activate", { params: { path: { id } } }));

export const deleteMember = (id: number) =>
  callApi<void>(() => api.DELETE("/api/members/{id}", { params: { path: { id } } }));

/** Active fixed desks with the member who has each one (choices for the member form). */
export const listFixedDesks = () =>
  callApi<FixedDeskOption[]>(() => api.GET("/api/members/fixed-desks"));

/** Sells a membership at the desk; it is Unpaid until the payment is recorded (BR05). */
export const sellMembership = (memberId: number, membershipTypeId: number) =>
  callApi<Membership>(() =>
    api.POST("/api/members/{id}/memberships", {
      params: { path: { id: memberId } },
      body: { membershipTypeId },
    }),
  );

/** "Amra Hodžić" – for headings, confirm texts and notifications. */
export const memberName = (member: Pick<Member, "firstName" | "lastName">) =>
  `${member.firstName} ${member.lastName}`;
