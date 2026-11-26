// Calls to /api/workspaces (FR09, manager only). Every function returns an ApiResult, never throws.
import { api } from "../../api/client";
import { callApi } from "../../api/result";
import type { components, paths } from "../../api/schema";

export type Workspace = components["schemas"]["WorkspaceResponse"];
export type WorkspaceType = components["schemas"]["WorkspaceType"];
export type WorkspacePage = components["schemas"]["PagedResultOfWorkspaceResponse"];
export type CreateWorkspaceRequest = components["schemas"]["CreateWorkspaceRequest"];
export type UpdateWorkspaceRequest = components["schemas"]["UpdateWorkspaceRequest"];

/** ?RoomId=&Type=&IsActive=&Search=&Page=&PageSize= – names exactly as the API expects them. */
export type WorkspaceListQuery = NonNullable<
  paths["/api/workspaces"]["get"]["parameters"]["query"]
>;

export const listWorkspaces = (query: WorkspaceListQuery) =>
  callApi<WorkspacePage>(() => api.GET("/api/workspaces", { params: { query } }));

export const createWorkspace = (body: CreateWorkspaceRequest) =>
  callApi<Workspace>(() => api.POST("/api/workspaces", { body }));

export const updateWorkspace = (id: number, body: UpdateWorkspaceRequest) =>
  callApi<Workspace>(() => api.PUT("/api/workspaces/{id}", { params: { path: { id } }, body }));

export const deactivateWorkspace = (id: number) =>
  callApi<void>(() => api.POST("/api/workspaces/{id}/deactivate", { params: { path: { id } } }));

export const activateWorkspace = (id: number) =>
  callApi<void>(() => api.POST("/api/workspaces/{id}/activate", { params: { path: { id } } }));

export const deleteWorkspace = (id: number) =>
  callApi<void>(() => api.DELETE("/api/workspaces/{id}", { params: { path: { id } } }));
