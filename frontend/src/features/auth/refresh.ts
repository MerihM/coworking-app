// Gets a new access token from the httpOnly refresh cookie: POST /api/auth/refresh.
//
// Two protections against refreshing twice at the same time – which the backend would treat as a
// stolen cookie (reuse detection) and answer by logging the user out everywhere:
//
// 1. Single-flight (inside one tab): if a refresh is already running, every other caller gets the
//    SAME promise instead of starting a second request. Happens when several API calls hit 401
//    at once.
// 2. Web Locks API (across tabs): navigator.locks.request("cw-refresh") lets only one tab at a
//    time run a refresh. The second tab waits; by then the browser already holds the rotated
//    cookie from the first tab, so its refresh is valid too.
import { currentLanguage } from "../../i18n";
import { sessionStore, type Session } from "./sessionStore";

const LOCK_NAME = "cw-refresh";

let inFlight: Promise<Session | null> | null = null;

/**
 * Refreshes the session. Resolves with the new session, or null when the user must log in again
 * (no cookie, expired, revoked). Safe to call from many places at once.
 */
export function refreshSession(): Promise<Session | null> {
  inFlight ??= withCrossTabLock(requestNewSession).finally(() => {
    inFlight = null;
  });
  return inFlight;
}

async function requestNewSession(): Promise<Session | null> {
  let response: Response;
  try {
    // Plain fetch, not the typed `api` client: the client's middleware calls refreshSession()
    // itself, and a refresh must never trigger another refresh.
    // No body: the browser sends the cw_refresh cookie automatically (same origin, Path=/api/auth).
    response = await fetch("/api/auth/refresh", {
      method: "POST",
      credentials: "same-origin",
      headers: { "Accept-Language": currentLanguage() },
    });
  } catch {
    // Network error (API down, offline). Keep the current state; the request that needed the
    // refresh fails on its own and the user can try again.
    return null;
  }

  if (!response.ok) {
    // 401 auth.refresh_invalid: the session is over. The backend already deleted the cookie.
    sessionStore.set(null);
    return null;
  }

  const session = (await response.json()) as Session;
  sessionStore.set(session);
  return session;
}

function withCrossTabLock<T>(work: () => Promise<T>): Promise<T> {
  // Web Locks are supported by all current browsers, but not by jsdom (tests) – fall back to
  // running directly there.
  if (typeof navigator !== "undefined" && "locks" in navigator && navigator.locks) {
    return navigator.locks.request(LOCK_NAME, work);
  }
  return work();
}
