// Typed API client. "paths" is generated from the backend's OpenAPI document (npm run gen:api),
// so every call is checked by TypeScript: wrong URL, missing parameter or wrong body = compile error.
//
// Usage (from Sprint 2 on), e.g.:
//   const { data, error } = await api.GET("/api/workspaces", { params: { query: { page: 1 } } });
// "error" contains the ProblemDetails JSON from the backend (CW-5).
import createClient from "openapi-fetch";
import type { paths } from "./schema";

/** Relative base URL: requests go to the same origin (Vite proxy in dev, Nginx in production). */
export const api = createClient<paths>({ baseUrl: "/" });
