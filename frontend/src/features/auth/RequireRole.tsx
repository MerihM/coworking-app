import { Navigate, Outlet, useMatches } from "react-router";
import { FullScreenLoader } from "../../components/FullScreenLoader";
import type { Role } from "./roles";
import { useAuth } from "./useAuth";

/** What a route can declare in its `handle`, e.g. { roles: [Roles.Administrator] }. */
export type RouteHandle = {
  roles?: readonly Role[];
};

/**
 * Route guard: child routes only for the roles listed in the route's `handle.roles`.
 * Place it INSIDE RequireAuth (it assumes someone is logged in). Wrong role → /forbidden.
 *
 * Example in routes/router.ts:
 *   { Component: RequireRole, handle: { roles: [Roles.Administrator] }, children: [...] }
 *
 * The roles live in `handle` (route data) because router.ts uses Component: instead of JSX,
 * so the guard can't receive them as props. This is only for the UI: the API checks the same
 * rule again (NFR01) – hiding a page is not security, the 403 from the API is.
 */
export function RequireRole() {
  const { status, user } = useAuth();
  const matches = useMatches();

  if (status === "loading") {
    return <FullScreenLoader />;
  }

  // Every matched route that declares roles must allow the user's role.
  const requiredRoleLists = matches
    .map((match) => (match.handle as RouteHandle | undefined)?.roles)
    .filter((roles): roles is readonly Role[] => roles !== undefined);

  const allowed =
    user !== null && requiredRoleLists.every((roles) => roles.some((role) => role === user.role));

  return allowed ? <Outlet /> : <Navigate to="/forbidden" replace />;
}
