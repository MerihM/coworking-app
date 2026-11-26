import { useEffect, useMemo, useState, useSyncExternalStore, type ReactNode } from "react";
import { login, logout, register } from "./authApi";
import { AuthContext, type AuthContextValue } from "./authContext";
import { refreshSession } from "./refresh";
import { sessionStore } from "./sessionStore";

/** Refresh this long BEFORE the access token expires, so normal requests rarely see a 401. */
const REFRESH_BEFORE_EXPIRY_MS = 60_000;

/** Never schedule refreshes closer than this – protects against a loop if the PC clock is wrong. */
const MIN_REFRESH_DELAY_MS = 10_000;

/**
 * Provides the auth state to the whole app.
 *
 * On start it tries a silent login (POST /api/auth/refresh with the httpOnly cookie): after a page
 * reload the in-memory token is gone, but the cookie is still there. Until that answer arrives,
 * status is "loading" and the route guards show a spinner instead of redirecting to /login.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  // Re-renders whenever sessionStore.set(...) is called – by login, logout, or the API middleware.
  const session = useSyncExternalStore(sessionStore.subscribe, sessionStore.get);
  const [initialized, setInitialized] = useState(false);

  // Silent login once on start. In development StrictMode runs this effect twice; both runs get
  // the same promise from refreshSession() (single-flight), so only ONE request is sent.
  useEffect(() => {
    let active = true;
    void refreshSession().finally(() => {
      if (active) {
        setInitialized(true);
      }
    });
    return () => {
      active = false;
    };
  }, []);

  // Proactive refresh ~1 minute before the access token expires.
  useEffect(() => {
    if (!session) {
      return;
    }
    const expiresAt = new Date(session.accessTokenExpiresAt).getTime();
    const delay = Math.max(expiresAt - Date.now() - REFRESH_BEFORE_EXPIRY_MS, MIN_REFRESH_DELAY_MS);
    const timer = window.setTimeout(() => void refreshSession(), delay);
    return () => window.clearTimeout(timer);
  }, [session]);

  const value = useMemo<AuthContextValue>(
    () => ({
      status: !initialized ? "loading" : session ? "authenticated" : "anonymous",
      user: session?.user ?? null,
      login,
      register,
      logout,
    }),
    [initialized, session],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
