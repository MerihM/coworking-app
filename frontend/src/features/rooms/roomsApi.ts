// Calls to /api/rooms (FR09, manager only). Same shape as employeesApi: every function returns an
// ApiResult instead of throwing.
import { api } from "../../api/client";
import { callApi } from "../../api/result";
import type { components, paths } from "../../api/schema";

export type Room = components["schemas"]["RoomResponse"];
export type RoomPage = components["schemas"]["PagedResultOfRoomResponse"];
export type CreateRoomRequest = components["schemas"]["CreateRoomRequest"];
export type UpdateRoomRequest = components["schemas"]["UpdateRoomRequest"];

/** ?Page=&PageSize=&Search=&IsActive= – names exactly as the API expects them. */
export type RoomListQuery = NonNullable<paths["/api/rooms"]["get"]["parameters"]["query"]>;

export const listRooms = (query: RoomListQuery) =>
  callApi<RoomPage>(() => api.GET("/api/rooms", { params: { query } }));

export const getRoom = (id: number) =>
  callApi<Room>(() => api.GET("/api/rooms/{id}", { params: { path: { id } } }));

export const createRoom = (body: CreateRoomRequest) =>
  callApi<Room>(() => api.POST("/api/rooms", { body }));

export const updateRoom = (id: number, body: UpdateRoomRequest) =>
  callApi<Room>(() => api.PUT("/api/rooms/{id}", { params: { path: { id } }, body }));

export const deactivateRoom = (id: number) =>
  callApi<void>(() => api.POST("/api/rooms/{id}/deactivate", { params: { path: { id } } }));

export const activateRoom = (id: number) =>
  callApi<void>(() => api.POST("/api/rooms/{id}/activate", { params: { path: { id } } }));

export const deleteRoom = (id: number) =>
  callApi<void>(() => api.DELETE("/api/rooms/{id}", { params: { path: { id } } }));
