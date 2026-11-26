// The current login session, kept ONLY in JavaScript memory (ADR 0003).
//
// Why not localStorage? Anything in localStorage can be read by any script on the page, so a
// single XSS bug (or a compromised npm package) could steal the token. Memory is wiped on every
// page reload – that's fine: the httpOnly refresh cookie brings the session back (silent login).
//
// This is a tiny "external store": plain variables plus subscribe/notify. React components read
// it through useSyncExternalStore (AuthProvider), and non-React code (the API middleware) reads
// it directly with sessionStore.get().
import type { components } from "../../api/schema";

/** What login/register/refresh return: access token, its expiry and the user. */
export type Session = components["schemas"]["AuthResponse"];

/** The logged-in user: id, userType, role, email, fullName. */
export type CurrentUser = components["schemas"]["UserResponse"];

type Listener = () => void;

let current: Session | null = null;
const listeners = new Set<Listener>();

export const sessionStore = {
  /** The current session, or null when nobody is logged in. */
  get(): Session | null {
    return current;
  },

  /** Replaces the session (null = logged out) and tells every subscriber. */
  set(next: Session | null): void {
    current = next;
    listeners.forEach((listener) => listener());
  },

  /** Registers a listener; returns the function that removes it (useSyncExternalStore contract). */
  subscribe(listener: Listener): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
};
