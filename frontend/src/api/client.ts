// Typed API client. "paths" is generated from the backend's OpenAPI document (npm run gen:api),
// so every call is checked by TypeScript: wrong URL, missing parameter or wrong body = compile error.
//
// Usage, e.g.:
//   const { data, error } = await api.GET("/api/employees", { params: { query: { Page: 1 } } });
// "error" contains the ProblemDetails JSON from the backend (with "code" for translation).
import createClient from "openapi-fetch";
import { currentLanguage } from "../i18n";
import type { paths } from "./schema";

/** Relative base URL: requests go to the same origin (Vite proxy in dev, Nginx in production). */
export const api = createClient<paths>({ baseUrl: "/" });

// Middleware = code that runs for EVERY request made with `api`.
// Accept-Language tells the backend which language to use for validation messages (CW-11), so
// "'Lozinka' ne smije biti prazno." comes back when the UI is in Bosnian.
// CW-15 adds a second middleware here for the access token and automatic refresh.
api.use({
  onRequest({ request }) {
    request.headers.set("Accept-Language", currentLanguage());
    return request;
  },
});
