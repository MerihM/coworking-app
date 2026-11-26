// Calls to /api/employees (FR12, administrator only). Each function returns a small result object
// instead of throwing, so pages can show the error next to the right control.
import { api } from "../../api/client";
import { callApi as call } from "../../api/result";
import type { components, paths } from "../../api/schema";

// ApiResult moved to api/result.ts in CW-23; re-exported so existing imports keep working.
export type { ApiResult } from "../../api/result";

export type Employee = components["schemas"]["EmployeeResponse"];
export type EmployeeRole = components["schemas"]["EmployeeRole"];
export type EmployeePage = components["schemas"]["PagedResultOfEmployeeResponse"];
export type CreateEmployeeRequest = components["schemas"]["CreateEmployeeRequest"];
export type UpdateEmployeeRequest = components["schemas"]["UpdateEmployeeRequest"];
export type ResetPasswordRequest = components["schemas"]["ResetPasswordRequest"];

/** ?Page=&PageSize=&Search=&Role=&IsActive= – names exactly as the API expects them. */
export type EmployeeListQuery = NonNullable<paths["/api/employees"]["get"]["parameters"]["query"]>;

/** All staff roles, in the order shown in selects. */
export const employeeRoles = [
  "Receptionist",
  "Manager",
  "Administrator",
] as const satisfies readonly EmployeeRole[];

export const listEmployees = (query: EmployeeListQuery) =>
  call<EmployeePage>(() => api.GET("/api/employees", { params: { query } }));

export const createEmployee = (body: CreateEmployeeRequest) =>
  call<Employee>(() => api.POST("/api/employees", { body }));

export const updateEmployee = (id: number, body: UpdateEmployeeRequest) =>
  call<Employee>(() => api.PUT("/api/employees/{id}", { params: { path: { id } }, body }));

export const deactivateEmployee = (id: number) =>
  call<void>(() => api.POST("/api/employees/{id}/deactivate", { params: { path: { id } } }));

export const activateEmployee = (id: number) =>
  call<void>(() => api.POST("/api/employees/{id}/activate", { params: { path: { id } } }));

export const resetEmployeePassword = (id: number, body: ResetPasswordRequest) =>
  call<void>(() =>
    api.POST("/api/employees/{id}/reset-password", { params: { path: { id } }, body }),
  );

export const deleteEmployee = (id: number) =>
  call<void>(() => api.DELETE("/api/employees/{id}", { params: { path: { id } } }));

/** "Emir Kovač" – for headings, confirm texts and notifications. */
export const fullName = (employee: Pick<Employee, "firstName" | "lastName">) =>
  `${employee.firstName} ${employee.lastName}`;
