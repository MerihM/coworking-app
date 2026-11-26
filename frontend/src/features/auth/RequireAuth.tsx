import { Navigate, Outlet, useLocation } from "react-router";
import { FullScreenLoader } from "../../components/FullScreenLoader";
import { useAuth } from "./useAuth";

/**
 * Route guard: child routes only for logged-in users. Anyone else goes to
 * /login?from=<this page>, and comes back here after logging in.
 *
 * Used as a "layout route" without a path in routes/router.ts: it renders <Outlet /> (the child
 * page) when allowed, or a redirect when not.
 */
export function RequireAuth() {
  const { status } = useAuth();
  const location = useLocation();

  if (status === "loading") {
    // Don't redirect yet: after a reload the silent login may still succeed.
    return <FullScreenLoader />;
  }

  if (status === "anonymous") {
    const from = location.pathname + location.search;
    // replace: the protected URL doesn't stay in history, so "Back" doesn't loop through it.
    return <Navigate to={`/login?from=${encodeURIComponent(from)}`} replace />;
  }

  return <Outlet />;
}
