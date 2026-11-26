// Typed API client. "paths" is generated from the backend's OpenAPI document (npm run gen:api),
// so every call is checked by TypeScript: wrong URL, missing parameter or wrong body = compile error.
//
// Usage, e.g.:
//   const { data, error } = await api.GET("/api/employees", { params: { query: { Page: 1 } } });
// "error" contains the ProblemDetails JSON from the backend – see toProblem() in problem.ts.
import createClient, { type Middleware } from "openapi-fetch";
import { refreshSession } from "../features/auth/refresh";
import { sessionStore } from "../features/auth/sessionStore";
import { currentLanguage } from "../i18n";
import type { paths } from "./schema";

/**
 * Same origin as the page (Vite proxy in dev, Nginx in production), written as an absolute URL
 * (http://localhost:5173) rather than "/" so the client also works in tests, where Node's fetch
 * doesn't accept relative URLs.
 */
export const api = createClient<paths>({ baseUrl: window.location.origin });

/**
 * Accept-Language on every request, so the backend answers validation messages in the UI
 * language ("'Lozinka' ne smije biti prazno.").
 */
const languageMiddleware: Middleware = {
  onRequest({ request }) {
    request.headers.set("Accept-Language", currentLanguage());
    return request;
  },
};

/** Auth endpoints never get the refresh-and-retry treatment (a failed login is just a failed login). */
const NO_RETRY_PATHS = new Set([
  "/api/auth/login",
  "/api/auth/register",
  "/api/auth/refresh",
  "/api/auth/logout",
]);

/**
 * A copy of each request taken BEFORE it is sent. A Request body can be read only once, and fetch
 * reads it, so to repeat a POST after a refresh we need this untouched copy. WeakMap: the copy
 * disappears together with the original request.
 */
const retryCopies = new WeakMap<Request, Request>();

/**
 * Bearer token + automatic refresh:
 * 1. onRequest: add "Authorization: Bearer <access token>" when logged in.
 * 2. onResponse: on 401 (access token expired), refresh once and repeat the request with the new
 *    token. If the refresh fails, the 401 goes back to the caller and the session is cleared,
 *    so route guards send the user to the login page.
 */
const authMiddleware: Middleware = {
  onRequest({ request }) {
    const accessToken = sessionStore.get()?.accessToken;
    if (accessToken) {
      request.headers.set("Authorization", `Bearer ${accessToken}`);
    }

    if (!NO_RETRY_PATHS.has(new URL(request.url).pathname)) {
      retryCopies.set(request, request.clone());
    }
    return request;
  },

  async onResponse({ request, response }) {
    const copy = retryCopies.get(request);
    if (response.status !== 401 || !copy) {
      return response;
    }

    const session = await refreshSession();
    if (!session) {
      return response;
    }

    // The repeated request goes straight to fetch, not through the middleware again, so a second
    // 401 is returned as it is – no endless refresh loop.
    copy.headers.set("Authorization", `Bearer ${session.accessToken}`);
    return fetch(copy);
  },
};

api.use(languageMiddleware, authMiddleware);
